using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;

namespace MyAwesomeWhitelist.Core
{
    /// <summary>
    /// Persistent mapping from SteamID64 → display name, stored as a JSON file
    /// under <c>BepInEx/config/MyAwesomeWhitelist/names.json</c>. Updated every
    /// time a player's name is observed via <see cref="SessionTracker"/> (P4
    /// OnClientHelo postfix). Used everywhere in the UI so the user never sees a
    /// raw SteamID — even for offline / manually-added entries.
    ///
    /// Format: <code>{"76561198000000000":"PlayerName",...}</code>
    /// </summary>
    internal static class NameCache
    {
        private const string FileName = "names.json";
        private static readonly object _lock = new object();
        private static Dictionary<string, string> _cache = new Dictionary<string, string>();
        private static bool _dirty;

        public static void Init()
        {
            Load();
        }

        /// <summary>Record or update a player's name. Called from P4 OnClientHelo.</summary>
        public static void Record(string steamId, string name)
        {
            if (string.IsNullOrEmpty(steamId))
                return;
            lock (_lock)
            {
                string existing;
                if (!_cache.TryGetValue(steamId, out existing) || existing != name)
                {
                    _cache[steamId] = name ?? string.Empty;
                    _dirty = true;
                }
            }
        }

        /// <summary>
        /// Resolve a SteamID to its best-known display name. Checks the local
        /// cache first; on miss, queries <c>SteamFriends.GetFriendPersonaName</c>.
        /// A successful Steam lookup is cached immediately so subsequent calls
        /// are instant. Returns "(未知)" only when both cache and Steam API fail.
        /// </summary>
        public static string Resolve(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return "(未知)";
            // 1) Local cache hit → return instantly.
            lock (_lock)
            {
                string name;
                if (_cache.TryGetValue(steamId, out name) && !string.IsNullOrEmpty(name))
                    return name;
            }
            // 2) Cache miss → ask Steamworks for the display name.
            //    GetFriendPersonaName works for any player who is (or was recently)
            //    in a P2P session with us, plus all Steam friends.
            ulong id;
            if (!ulong.TryParse(steamId, out id))
                return steamId; // opaque non-numeric key
            try
            {
                var csid = new Steamworks.CSteamID(id);
                string persona = Steamworks.SteamFriends.GetFriendPersonaName(csid);
                // Steam returns the numeric ID as string for users it knows nothing about;
                // treat that as "still unknown" rather than showing digits.
                if (!string.IsNullOrEmpty(persona) && !ulong.TryParse(persona, out _))
                {
                    Record(steamId, persona); // cache the discovered name
                    return persona;
                }
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"Steam name lookup failed for {steamId}: {e.Message}");
            }
            // 3) Nothing worked — show truncated hint so user can still identify which entry.
            return steamId.Length > 12 ? "…" + steamId.Substring(steamId.Length - 8) : steamId;
        }

        /// <summary>Reverse lookup: find SteamID(s) by display name (for manual-add resolution).</summary>
        public static List<string> FindByName(string name)
        {
            var results = new List<string>();
            if (string.IsNullOrEmpty(name))
                return results;
            string lower = name.ToLowerInvariant();
            lock (_lock)
            {
                foreach (var kv in _cache)
                {
                    if (!string.IsNullOrEmpty(kv.Value) && kv.Value.ToLowerInvariant() == lower)
                        results.Add(kv.Key);
                }
            }
            return results;
        }

        /// <summary>Partial reverse lookup: names containing the query.</summary>
        public static List<string> FindByNameContains(string name)
        {
            var results = new List<string>();
            if (string.IsNullOrEmpty(name))
                return results;
            string lower = name.ToLowerInvariant();
            lock (_lock)
            {
                foreach (var kv in _cache)
                {
                    if (!string.IsNullOrEmpty(kv.Value) && kv.Value.ToLowerInvariant().Contains(lower))
                        results.Add(kv.Key);
                }
            }
            return results;
        }

        /// <summary>Save to disk (call on changes + on quit).</summary>
        public static void Save()
        {
            lock (_lock)
            {
                if (!_dirty) return;
                _dirty = false;
            }
            try
            {
                string dir = Path.Combine(Paths.ConfigPath, PluginInfo.PLUGIN_NAME);
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, FileName);

                var sb = new StringBuilder();
                sb.Append('{');
                bool first = true;
                Dictionary<string, string> snapshot;
                lock (_lock) { snapshot = new Dictionary<string, string>(_cache); }
                foreach (var kv in snapshot)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(JsonQuote(kv.Key));
                    sb.Append(':');
                    sb.Append(JsonQuote(kv.Value));
                }
                sb.Append('}');

                string tmp = path + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Failed to save NameCache: {e.Message}");
            }
        }

        /// <summary>Load from disk (called on init).</summary>
        private static void Load()
        {
            try
            {
                string path = Path.Combine(Paths.ConfigPath, PluginInfo.PLUGIN_NAME, FileName);
                if (!File.Exists(path)) return;
                string json = File.ReadAllText(path, Encoding.UTF8);
                var dict = ParseObject(json);
                lock (_lock) { _cache = dict ?? new Dictionary<string, string>(); }
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"Failed to load NameCache: {e.Message}");
            }
        }

        // ── Minimal JSON parser for { "key": "value", ... } ────────────

        private static Dictionary<string, string> ParseObject(string json)
        {
            int i = 0;
            int len = json.Length;
            var dict = new Dictionary<string, string>();

            SkipWs(json, ref i);
            if (i >= len || json[i] != '{') return null;
            i++; // skip {

            while (true)
            {
                SkipWs(json, ref i);
                if (i >= len || json[i] == '}') break;
                if (dict.Count > 0)
                {
                    if (json[i] == ',') i++;
                    else break; // malformed
                    SkipWs(json, ref i);
                }
                string key = ReadString(json, ref i);
                SkipWs(json, ref i);
                if (i >= len || json[i] != ':') break;
                i++; // skip :
                SkipWs(json, ref i);
                string value = ReadString(json, ref i);
                dict[key] = value;
                SkipWs(json, ref i);
                if (i < len && json[i] == ',') continue;
            }
            return dict;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        private static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return "";
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    i++;
                    c = s[i];
                    switch (c)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(c); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
                i++;
            }
            if (i < s.Length) i++; // closing "
            return sb.ToString();
        }

        private static string JsonQuote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            for (int j = 0; j < s.Length; j++)
            {
                char c = s[j];
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
