using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using MyAwesomeWhitelist.Core;
using MyAwesomeWhitelist.I18n;
using MyAwesomeWhitelist.Lists;
using Multiplayer;
using Steamworks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyAwesomeWhitelist.UI
{
    /// <summary>
    /// The in-game manager window (IMGUI): overlay panel with tabs — players,
    /// whitelist, blacklist, friends, settings. All UI text goes through
    /// <see cref="LocalizationService"/> so switching language takes effect
    /// immediately.
    ///
    /// <b>Input model (proven by TwilightCore.ChatView):</b>
    /// Open via Update/GetKeyDown; close via OnGUI/Event.current (works even
    /// when TextField has focus); uses BeginArea not GUI.Window.
    ///
    /// <b>Mouse:</b> opening the window also enters the game's pause-menu
    /// state (MenuSystem.ShowPauseMenu), which makes MenuSystem.BindCursor
    /// unlock the cursor — the plain Cursor.visible override is fought every
    /// frame by MenuSystem.OnGUI and loses.
    /// </summary>
    internal sealed class WhitelistWindow : MonoBehaviour
    {
        private bool _open;
        private Rect _rect = new Rect(16f, 16f, 620f, 560f);
        private readonly Styles _styles = new Styles();
        private Hotkey _hotkey;
        private int _tab;
        private string[] _tabs; // filled from L in Awake
        private Vector2 _scroll;

        private int _whiteWhich, _blackWhich; // list tab: 0 = permanent, 1 = temporary
        private Vector2 _whiteListScroll, _blackListScroll;
        private string _whiteInput = string.Empty, _blackInput = string.Empty;
        private string _hotkeyInput;
        private string _hotkeyError;

        private bool _langOpen;      // language dropdown popup visible
        private Rect _langPopupRect;

        private string _toast;
        private float _toastUntil;

        private bool _pauseShown;    // we opened the Esc pause menu for cursor control
        private float _nextSweep;    // auto blacklist enforcement timer
        private float _friendCacheUntil;
        private List<FriendEntry> _friendCache;
        private string _friendQuery = string.Empty;

        // Fullscreen invisible uGUI blocker raised while the window is open:
        // IMGUI does not participate in uGUI raycasting, so without this the
        // pause menu behind the window still receives clicks through it.
        private GameObject _blocker;
        private BlockCursorRaycasts _blockerBehaviour;

        // ── Shortcut: L for LocalizationService.Instance.Get() ──
        private static string L(string key) => LocalizationService.Instance.Get(key);
        private static string LF(string key, params object[] args) => string.Format(LocalizationService.Instance.Get(key), args);

        // ── Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            var loc = LocalizationService.Instance;
            _tabs = new[]
            {
                L("TAB_PLAYERS"), L("TAB_WHITELIST"), L("TAB_BLACKLIST"),
                L("TAB_FRIENDS"), L("TAB_SETTINGS"),
            };
            _hotkey = new Hotkey(Plugin.Config.ToggleWindowHotkey.Value);
            _hotkeyInput = Plugin.Config.ToggleWindowHotkey.Value;
        }

        private void Update()
        {
            KickService.DrainMainThreadQueue();
            if (!_open && _hotkey != null && _hotkey.IsDown)
                Open();

            // Auto blacklist enforcement: kick blacklisted players already in
            // the lobby every second — no manual button needed.
            if (Time.unscaledTime >= _nextSweep)
            {
                _nextSweep = Time.unscaledTime + 1f;
                SweepBlacklist();
            }
        }

        private void OnGUI()
        {
            _styles.Ensure();
            if (!_open) return;

            // Close detection — MUST be first.
            if (_open && Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Escape
                    || (_hotkey != null && _hotkey.Matches(Event.current)))
                { Close(); Event.current.Use(); return; }
            }

            try { MenuSystem.keyboardState = KeyboardState.NetChat; } catch { }
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            GUI.color = Color.white;
            GUILayout.BeginArea(_rect, _styles.Box);
            DrawContent();
            if (_langOpen) DrawLangPopup(); // last, so it draws over the tab content
            GUILayout.EndArea();

            // Track the window rect so the uGUI blocker leaves exactly this
            // area clickable (nothing behind it responds through the panel).
            if (_blockerBehaviour != null)
            {
                _blockerBehaviour.WindowRect = _rect;
            }
        }

        private void Open()
        {
            _open = true;
            _langOpen = false;
            ListService.Instance?.ReloadFromDisk();
            EnsureBlocker();
            if (_blocker != null) _blocker.SetActive(true);
            TryEnterPauseMenu();
        }

        /// <summary>(Re)create the fullscreen uGUI click blocker. Rendered last
        /// (highest sibling index) so it sits above the pause menu, with
        /// raycastTarget on — the EventSystem then reports a hit everywhere and
        /// no uGUI control behind our IMGUI window can be clicked.</summary>
        private void EnsureBlocker()
        {
            if (_blocker != null)
            {
                return;
            }
            try
            {
                var canvas = FindObjectOfType<Canvas>();
                if (canvas == null)
                {
                    return; // no uGUI alive — nothing to block
                }
                _blocker = new GameObject("MyAwesomeWhitelist.ClickBlocker");
                _blocker.transform.SetParent(canvas.transform, false);
                var img = _blocker.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0f); // fully transparent
                img.raycastTarget = true;
                _blockerBehaviour = _blocker.AddComponent<BlockCursorRaycasts>();
                var rt = img.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                _blocker.transform.SetAsLastSibling();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"[UI] Click blocker setup failed: {e.Message}");
                _blocker = null;
            }
        }

        /// <summary>Keeps the blocker on top of any menu that appears later
        /// and swallows cursor interactions outside the window rect — IMGUI
        /// receives events regardless, so this only affects uGUI.</summary>
        private sealed class BlockCursorRaycasts : UIBehaviour, ICanvasRaycastFilter
        {
            internal Rect WindowRect; // screen rect of our IMGUI window

            public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
            {
                // Valid (clickable) only outside our window — everywhere else the
                // raycast stops at this blocker, so uGUI controls behind the
                // window can no longer be clicked through it.
                return !WindowRect.Contains(screenPoint);
            }

            private void LateUpdate()
            {
                // Reassert top-most in case a menu was opened after us.
                transform.SetAsLastSibling();
            }
        }

        /// <summary>Enter the Esc pause-menu state so the cursor is usable.
        /// BindCursor only frees the mouse while a menu is active, so fighting
        /// it with Cursor.visible alone never wins.</summary>
        private void TryEnterPauseMenu()
        {
            try
            {
                var ms = MenuSystem.instance;
                if (ms == null || Game.instance == null) return;  // not in a level — cursor already free
                if (ms.state != MenuSystemState.Inactive) return; // some menu is already up
                ms.ShowPauseMenu();
                _pauseShown = true;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"[UI] ShowPauseMenu failed: {e.Message}");
            }
        }

        private void Close()
        {
            _open = false;
            _langOpen = false;
            GUIUtility.keyboardControl = 0;
            if (_blocker != null) _blocker.SetActive(false);
            if (_pauseShown)
            {
                _pauseShown = false;
                try { MenuSystem.instance?.HideMenus(); } catch { }
            }
            try { MenuSystem.keyboardState = KeyboardState.None; } catch { }
        }

        private void Toast(string key, params object[] args)
        {
            _toast = LF(key, args);
            _toastUntil = Time.realtimeSinceStartup + 3f;
        }
        private void ToastRaw(string msg) { _toast = msg; _toastUntil = Time.realtimeSinceStartup + 3f; }

        private static bool IsHost => NetGame.instance != null && NetGame.isServer && NetGame.isNetStarted;

        // ── Content layout ─────────────────────────────────────────────

        private void DrawContent()
        {
            var lists = ListService.Instance;

            GUILayout.BeginVertical();

            // Title bar with drag handle
            GUILayout.BeginHorizontal();
            GUILayout.Label($"  MyAwesomeWhitelist v{PluginInfo.PLUGIN_VERSION}", _styles.Header);
            Rect titleRect = GUILayoutUtility.GetLastRect();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(L("BTN_CLOSE"), _styles.MiniButton, GUILayout.Width(28), GUILayout.Height(22))) Close();
            GUILayout.EndHorizontal();
            // Drag window by dragging the title bar area (GetLastRect must be
            // captured inside the horizontal — after EndHorizontal it returns
            // the close button).
            if (Event.current.type == EventType.MouseDrag
                && new Rect(0f, titleRect.y - 2f, _rect.width - 32f, Mathf.Max(titleRect.height + 4f, 24f)).Contains(Event.current.mousePosition))
            {
                _rect.position += Event.current.delta;
                ClampToScreen();
                Event.current.Use();
            }

            _tab = GUILayout.Toolbar(_tab, _tabs, _styles.Button);

            // Status line — host state only; the whitelist/blacklist toggles
            // live on the Players tab, next to the player list they govern.
            GUILayout.BeginHorizontal();
            if (!IsHost)
            {
                var w = new GUIStyle(_styles.Small) { normal = { textColor = new Color(1f, 0.8f, 0.25f) } };
                GUILayout.Label(L("HOST_NOT"), w);
            }
            else
            {
                GUILayout.Label(L("HOST_IS"), _styles.Small);
            }
            GUILayout.EndHorizontal();

            // Settings tab is short and hosts the language dropdown popup —
            // no scroll view, so the popup is not clipped or offset.
            bool useScroll = _tab != 4;
            if (useScroll) _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            switch (_tab)
            {
                case 0: DrawPlayers(); break;
                case 1: DrawList(lists.PermWhite, lists.TempWhite, ref _whiteWhich, ref _whiteInput, ref _whiteListScroll, true); break;
                case 2: DrawList(lists.PermBlack, lists.TempBlack, ref _blackWhich, ref _blackInput, ref _blackListScroll, false); break;
                case 3: DrawFriends(); break;
                case 4: DrawSettings(); break;
            }
            if (useScroll) GUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_toast) && Time.realtimeSinceStartup < _toastUntil)
                GUILayout.Label(_toast, _styles.Small);

            GUILayout.EndVertical();
        }

        private void ClampToScreen()
        {
            _rect.x = Mathf.Clamp(_rect.x, -_rect.width + 80f, Screen.width - 80f);
            _rect.y = Mathf.Clamp(_rect.y, 0f, Screen.height - 40f);
        }

        // ── Tab 1: players ───────────────────────────────────────────

        private void DrawPlayers()
        {
            var cfg = Plugin.Config;

            // Toggles belong here, under the player list header — this page is
            // the single place where whitelist/blacklist state is controlled.
            GUILayout.BeginHorizontal();
            GUILayout.Label(LF("ONLINE_PLAYERS", CurrentPlayers().Count), _styles.Section, GUILayout.Width(160));
            GUILayout.FlexibleSpace();
            cfg.WhitelistEnabled.Value = GUILayout.Toggle(cfg.WhitelistEnabled.Value, L("WHITELIST_TOGGLE"), _styles.Toggle);
            GUILayout.Space(12);
            cfg.BlacklistEnabled.Value = GUILayout.Toggle(cfg.BlacklistEnabled.Value, L("BLACKLIST_TOGGLE"), _styles.Toggle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (IsHost && cfg.WhitelistEnabled.Value)
            {
                if (GUILayout.Button(L("ENFORCE_WHITELIST"), _styles.Button))
                    EnforceWhitelistOnCurrentPlayers();
            }
            if (IsHost && cfg.BlacklistEnabled.Value)
                GUILayout.Label(L("AUTO_BLACKLIST_NOTE"), _styles.Small);
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            GUI.enabled = IsHost;
            foreach (var p in CurrentPlayers())
            {
                GUILayout.BeginHorizontal();
                string display = Core.NameCache.Resolve(p.SteamId);
                if (!string.IsNullOrEmpty(p.Name) && p.Name != NetMsgId.AddPlayer.ToString()) display = p.Name;
                GUILayout.Label(display, _styles.Label, GUILayout.Width(150));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(L("BTN_KICK"), _styles.MiniButton, GUILayout.Width(40)))
                    { KickService.EnqueueForceKickBySteamId(p.SteamId, "manual kick"); ToastRaw(display + " → " + LF("TOAST_KICKED", display)); }
                if (GUILayout.Button(L("BTN_WHITE_PERM"), _styles.MiniButton, GUILayout.Width(54)))
                    { AddToList(ListService.Instance.PermWhite, p.SteamId, display, ListService.WhiteFile, true); }
                if (GUILayout.Button(L("BTN_WHITE_TEMP"), _styles.MiniButton, GUILayout.Width(54)))
                    { AddToList(ListService.Instance.TempWhite, p.SteamId, display, null, false); }
                if (GUILayout.Button(L("BTN_KICK_PERMBLACK"), _styles.MiniButton, GUILayout.Width(54)))
                    { Blacklist(p.SteamId, display, true); ToastRaw(display + " → " + LF("TOAST_KICKED_BLACKLISTED", L("LIST_PERM"), display)); }
                if (GUILayout.Button(L("BTN_KICK_TEMPBLACK"), _styles.MiniButton, GUILayout.Width(54)))
                    { Blacklist(p.SteamId, display, false); ToastRaw(display + " → " + LF("TOAST_KICKED_BLACKLISTED", L("LIST_TEMP"), display)); }
                // Remove from whichever list(s) the player is currently on.
                var ls = ListService.Instance;
                var snap = ls.Snapshot;
                bool inWhite = snap.PermWhite.Contains(p.SteamId) || snap.TempWhite.Contains(p.SteamId);
                bool inBlack = snap.PermBlack.Contains(p.SteamId) || snap.TempBlack.Contains(p.SteamId);
                if (inWhite && GUILayout.Button(L("BTN_UNWHITE"), _styles.MiniButton, GUILayout.Width(54)))
                {
                    RemoveEntry(ls.PermWhite, p.SteamId, display, true, true);
                    RemoveEntry(ls.TempWhite, p.SteamId, display, false, true);
                }
                if (inBlack && GUILayout.Button(L("BTN_UNBLACK"), _styles.MiniButton, GUILayout.Width(54)))
                {
                    RemoveEntry(ls.PermBlack, p.SteamId, display, true, false);
                    RemoveEntry(ls.TempBlack, p.SteamId, display, false, false);
                }
                GUILayout.EndHorizontal();
            }
            GUI.enabled = true;

            if (CurrentPlayers().Count == 0)
                GUILayout.Label(IsHost ? L("NO_PLAYERS") : L("WAITING_LOBBY"), _styles.Small);
        }

        /// <summary>Kick everyone currently in the lobby who is not on the
        /// whitelist (permanent or temporary).</summary>
        private void EnforceWhitelistOnCurrentPlayers()
        {
            var lists = ListService.Instance; if (lists == null) return;
            var snap = lists.Snapshot; int k = 0;
            foreach (var p in CurrentPlayers())
            {
                if (!snap.PermWhite.Contains(p.SteamId) && !snap.TempWhite.Contains(p.SteamId))
                { KickService.EnqueueForceKickBySteamId(p.SteamId, "whitelist enforcement"); k++; }
            }
            Toast(k > 0 ? "TOAST_ENFORCED_N" : "TOAST_NO_ENFORCED_W", k);
        }

        /// <summary>Background enforcement (every second, host only): kick any
        /// blacklisted player found in the lobby — replaces the old manual
        /// "enforce blacklist" button. The kick keeps the P2P ban (that's the
        /// point of a blacklist).</summary>
        private void SweepBlacklist()
        {
            try
            {
                var cfg = Plugin.Config;
                var lists = ListService.Instance;
                if (cfg == null || lists == null || !IsHost || !cfg.BlacklistEnabled.Value) return;
                var snap = lists.Snapshot;
                foreach (var p in CurrentPlayers())
                {
                    if (snap.PermBlack.Contains(p.SteamId) || snap.TempBlack.Contains(p.SteamId))
                        KickService.EnqueueBanKickBySteamId(p.SteamId, "auto blacklist sweep");
                }
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"[AutoBlacklist] sweep failed: {e.Message}");
            }
        }

        // ── Tab 2/3: whitelist / blacklist ─────────────────────────────

        private void DrawList(NameList perm, NameList temp, ref int which, ref string input, ref Vector2 listScroll, bool isWhite)
        {
            GUILayout.BeginHorizontal();
            which = GUILayout.Toolbar(which, new[] { L("LIST_PERM"), L("LIST_TEMP") }, _styles.Button, GUILayout.Width(200));
            GUILayout.Label(LF("LIST_COUNT", isWhite ? L("TAB_WHITELIST") : L("TAB_BLACKLIST"), perm.Count, temp.Count), _styles.Small);
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            var list = which == 0 ? perm : temp;

            // List box: plain auto-layout rows inside a scroll view, framed by
            // a Box via GUILayout. (A manual GetRect+BeginGroup combo draws the
            // rows into a nested layout context that never registers — rows
            // existed but were invisible.)
            listScroll = GUILayout.BeginScrollView(listScroll, _styles.ListBox, GUILayout.Height(220));
            if (list.Count == 0)
            {
                GUILayout.Space(8);
                GUILayout.Label(L("LIST_EMPTY"), _styles.Small);
            }
            var entries = list.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                GUILayout.BeginHorizontal();
                string display = Core.NameCache.Resolve(e.SteamId);
                if (display != e.Name && !string.IsNullOrEmpty(e.Name) && e.Name != L("UNKNOWN_PLAYER") && e.Name != L("PENDING_IDENTIFY"))
                    display = e.Name;
                GUILayout.Label(display, _styles.Label, GUILayout.Width(180));
                GUILayout.Label(e.AddedAt, _styles.Small, GUILayout.Width(110));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L("BTN_DELETE"), _styles.MiniButton, GUILayout.Width(44)))
                {
                    RemoveEntry(list, e.SteamId, e.Name, which == 0, isWhite);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            GUILayout.Label(L("MANUAL_ADD"), _styles.Section);
            GUILayout.BeginHorizontal();
            input = GUILayout.TextField(input, _styles.TextField, GUILayout.Width(240));
            string trimmed = input.Trim();
            GUI.enabled = !string.IsNullOrEmpty(trimmed);
            if (GUILayout.Button(L("MANUAL_ADD_PERM"), _styles.MiniButton, GUILayout.Width(48)))
                { ResolveAndAdd(perm, trimmed, isWhite ? ListService.WhiteFile : ListService.BlackFile, true); input = ""; }
            if (GUILayout.Button(L("MANUAL_ADD_TEMP"), _styles.MiniButton, GUILayout.Width(48)))
                { ResolveAndAdd(temp, trimmed, null, false); input = ""; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        // ── Tab 3: Steam friends ─────────────────────────────────────

        private void DrawFriends()
        {
            GUILayout.Label(L("FRIENDS_TITLE"), _styles.Section);
            GUILayout.Label(L("FRIENDS_DESC"), _styles.Small);

            GUILayout.BeginHorizontal();
            GUILayout.Label(L("FRIENDS_SEARCH"), _styles.Small, GUILayout.Width(110), GUILayout.Height(24));
            _friendQuery = GUILayout.TextField(_friendQuery, _styles.TextField, GUILayout.Width(220));
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            // Enumerating Steam friends is not cheap — cache the sorted list
            // for a few seconds instead of querying every OnGUI event.
            if (_friendCache == null || Time.realtimeSinceStartup >= _friendCacheUntil)
            {
                _friendCache = GetSteamFriends();
                _friendCache.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
                _friendCacheUntil = Time.realtimeSinceStartup + 5f;
            }
            string q = _friendQuery.Trim();

            // List management (add/remove) works everywhere — main menu
            // included. Only the kick part of "blacklist" is host-gated and
            // safely no-ops outside a lobby.
            int shown = 0;
            foreach (var f in _friendCache)
            {
                if (q.Length > 0 && !f.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase)) continue;
                shown++;
                GUILayout.BeginHorizontal();
                GUILayout.Label(f.Name, _styles.Label, GUILayout.Width(180));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L("BTN_WHITE_PERM"), _styles.MiniButton, GUILayout.Width(54)))
                    { AddToList(ListService.Instance.PermWhite, f.SteamId, f.Name, ListService.WhiteFile, true); Core.NameCache.Record(f.SteamId, f.Name); }
                if (GUILayout.Button(L("BTN_WHITE_TEMP"), _styles.MiniButton, GUILayout.Width(54)))
                    { AddToList(ListService.Instance.TempWhite, f.SteamId, f.Name, null, false); Core.NameCache.Record(f.SteamId, f.Name); }
                if (GUILayout.Button(L("BTN_BLACK_PERM"), _styles.MiniButton, GUILayout.Width(54)))
                    { Blacklist(f.SteamId, f.Name, true); Core.NameCache.Record(f.SteamId, f.Name); }
                if (GUILayout.Button(L("BTN_BLACK_TEMP"), _styles.MiniButton, GUILayout.Width(54)))
                    { Blacklist(f.SteamId, f.Name, false); Core.NameCache.Record(f.SteamId, f.Name); }
                GUILayout.EndHorizontal();
            }

            if (_friendCache.Count == 0) GUILayout.Label(L("NO_FRIENDS"), _styles.Small);
            else if (shown == 0) GUILayout.Label(L("LIST_EMPTY"), _styles.Small);
        }

        private static List<FriendEntry> GetSteamFriends()
        {
            var r = new List<FriendEntry>();
            try
            {
                int c = Steamworks.SteamFriends.GetFriendCount(Steamworks.EFriendFlags.k_EFriendFlagAll);
                for (int i = 0; i < c; i++)
                {
                    var fid = Steamworks.SteamFriends.GetFriendByIndex(i, Steamworks.EFriendFlags.k_EFriendFlagAll);
                    string n = Steamworks.SteamFriends.GetFriendPersonaName(fid);
                    if (!string.IsNullOrEmpty(n)) r.Add(new FriendEntry { Name = n, SteamId = fid.ToString() });
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning($"[I18n] Friends fail: {e.Message}"); }
            return r;
        }
        private sealed class FriendEntry { public string Name; public string SteamId; }

        // ── Tab 4: settings ────────────────────────────────────────────

        private void DrawSettings()
        {
            GUILayout.Label(L("SETTINGS_HOTKEY"), _styles.Section);
            GUILayout.Label(L("SETTINGS_HOTKEY_DESC"), _styles.Small);

            GUILayout.BeginHorizontal();
            _hotkeyInput = GUILayout.TextField(_hotkeyInput, _styles.TextField, GUILayout.Width(240));
            var cand = new Hotkey(_hotkeyInput);
            if (GUILayout.Button(L("SETTINGS_SAVE"), _styles.MiniButton, GUILayout.Width(48)))
            {
                if (cand.Valid) { Plugin.Config.ToggleWindowHotkey.Value = cand.ToString(); _hotkey = cand; _hotkeyError = null; Toast("SETTINGS_SAVED", cand.ToString()); }
                else _hotkeyError = L("SETTINGS_INVALID_KEY");
            }
            GUILayout.EndHorizontal();
            if (_hotkeyError != null)
            {
                var e = new GUIStyle(_styles.Small) { normal = { textColor = new Color(1f, 0.45f, 0.4f) } };
                GUILayout.Label(_hotkeyError, e);
            }

            GUILayout.Space(10);

            // Language dropdown (button + popup list, drawn after all content).
            GUILayout.Label(L("SETTINGS_LANG"), _styles.Section);
            var loc = LocalizationService.Instance;
            string[] codes = loc.AvailableCodes;
            int sel = System.Array.IndexOf(codes, loc.CurrentCode);
            if (sel < 0) sel = 0;
            string caption = loc.DisplayNameOf(codes[sel]) + "  ▾";
            Rect btn = GUILayoutUtility.GetRect(new GUIContent(caption), _styles.Button, GUILayout.Width(240f));
            if (GUI.Button(btn, caption, _styles.Button))
                _langOpen = !_langOpen;
            if (_langOpen)
                _langPopupRect = new Rect(btn.x, btn.yMax + 2f, 240f, 4f + 24f * codes.Length);

            GUILayout.Space(10);
            GUILayout.Label(L("SETTINGS_ABOUT"), _styles.Section);
            GUILayout.Label(L("SETTINGS_ABOUT_DESC"), _styles.Small);
            GUILayout.Label(L("SETTINGS_FILES"), _styles.Path);
            GUILayout.Label("  " + DisplayPath(ListStore.FilePath(ListService.WhiteFile)), _styles.Path);
            GUILayout.Label("  " + DisplayPath(ListStore.FilePath(ListService.BlackFile)), _styles.Path);
        }

        /// <summary>Language popup — drawn last inside the area so it floats
        /// above the settings content. Any click outside closes it.</summary>
        private void DrawLangPopup()
        {
            var loc = LocalizationService.Instance;
            string[] codes = loc.AvailableCodes;
            int sel = System.Array.IndexOf(codes, loc.CurrentCode);
            GUI.Box(_langPopupRect, GUIContent.none, _styles.Box);
            for (int i = 0; i < codes.Length; i++)
            {
                var r = new Rect(_langPopupRect.x + 2f, _langPopupRect.y + 2f + 24f * i, _langPopupRect.width - 4f, 22f);
                string label = (i == sel ? "✓ " : "") + loc.DisplayNameOf(codes[i]);
                if (GUI.Button(r, label, _styles.MiniButton) && i != sel)
                {
                    SetLanguage(codes[i]);
                }
            }
            if (Event.current.type == EventType.MouseDown && !_langPopupRect.Contains(Event.current.mousePosition))
                _langOpen = false;
        }

        private void SetLanguage(string code)
        {
            LocalizationService.Instance.SetLanguage(code);
            Plugin.Config.LanguageCode.Value = code;
            _langOpen = false;
            // Rebuild tab labels with the new language.
            _tabs = new[] { L("TAB_PLAYERS"), L("TAB_WHITELIST"), L("TAB_BLACKLIST"), L("TAB_FRIENDS"), L("TAB_SETTINGS") };
        }

        /// <summary>Show the path relative to the game root — the absolute
        /// path is longer than the window and wrapped into garbage.</summary>
        private static string DisplayPath(string absolute)
        {
            try
            {
                string root = Paths.GameRootPath;
                if (!string.IsNullOrEmpty(root))
                {
                    if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                        && !root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                        root += Path.DirectorySeparatorChar;
                    if (absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        return absolute.Substring(root.Length);
                }
            }
            catch { }
            return absolute;
        }

        /// <summary>Remove one entry from a name list: drop it, persist if
        /// permanent, rebuild the snapshot, and for blacklists also lift the
        /// P2P ban so the player can rejoin.</summary>
        private void RemoveEntry(NameList list, string steamId, string name, bool permanent, bool isWhite)
        {
            string display = Core.NameCache.Resolve(steamId);
            if (!string.IsNullOrEmpty(name) && name != L("UNKNOWN_PLAYER") && name != L("PENDING_IDENTIFY"))
                display = name;
            if (!list.Remove(steamId))
            {
                return;
            }
            if (permanent) ListStore.Save(isWhite ? ListService.WhiteFile : ListService.BlackFile, list);
            ListService.Instance.RebuildSnapshot();
            if (!isWhite)
            {
                KickService.UnbanReconnect(steamId);
                ListService.Instance.SyncKickedUsers(Plugin.Config.BlacklistEnabled.Value);
            }
            Toast("TOAST_DELETED", display);
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static bool ResolveInput(string input, out string steamId, out string displayName)
        {
            steamId = null; displayName = null;
            if (!ulong.TryParse(input.Trim(), out _)) return false;
            steamId = input.Trim();
            displayName = Core.NameCache.Resolve(steamId);
            if (displayName.StartsWith("…") || ulong.TryParse(displayName, out _))
                displayName = L("PENDING_IDENTIFY");
            return true;
        }

        private void ResolveAndAdd(NameList list, string input, string saveFile, bool permanent)
        {
            string sid, dn;
            if (!ResolveInput(input, out sid, out dn)) { Toast("INVALID_STEAMID"); return; }
            if (list.Add(ListEntry.Create(sid, dn)))
            {
                if (saveFile != null) ListStore.Save(saveFile, list);
                ListService.Instance.RebuildSnapshot();
                Toast("TOAST_ADDED", permanent ? L("LIST_PERM") : L("LIST_TEMP"), dn);
            }
            else { Toast("ALREADY_IN_LIST"); }
        }

        private void AddToList(NameList list, string steamId, string name, string saveFile, bool permanent)
        {
            if (list.Add(ListEntry.Create(steamId, name)))
            {
                if (saveFile != null) ListStore.Save(saveFile, list);
                ListService.Instance.RebuildSnapshot();
                Toast("TOAST_ADDED", permanent ? L("LIST_PERM") : L("LIST_TEMP"), name);
            }
            else { Toast("ALREADY_IN_LIST"); }
        }

        private void Blacklist(string steamId, string name, bool permanent)
        {
            var lists = ListService.Instance;
            var target = permanent ? lists.PermBlack : lists.TempBlack;
            if (target.Add(ListEntry.Create(steamId, name)))
            {
                if (permanent) lists.SaveBlacklist();
                ListService.Instance.RebuildSnapshot();
                KickService.BanReconnect(steamId);
                Toast("TOAST_BLACKLISTED", permanent ? L("LIST_PERM") : L("LIST_TEMP"), name);
            }
            else { Toast("ALREADY_IN_LIST"); }
            // Ban-kick: the kickedUsers entry stays so reconnects are refused.
            KickService.EnqueueBanKickBySteamId(steamId, "blacklisted");
        }

        private static List<SessionTracker.PlayerInfo> _playerCache;
        private static List<SessionTracker.PlayerInfo> CurrentPlayers()
        {
            if (_playerCache == null)
                _playerCache = SessionTracker.Instance != null
                    ? SessionTracker.Instance.Snapshot() : new List<SessionTracker.PlayerInfo>();
            return _playerCache;
        }

        private void LateUpdate() => _playerCache = null;
        private void OnApplicationQuit() => Core.NameCache.Save();
    }
}
