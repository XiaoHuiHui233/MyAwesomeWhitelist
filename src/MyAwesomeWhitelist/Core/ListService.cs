using System.Collections.Generic;
using MyAwesomeWhitelist.Lists;

namespace MyAwesomeWhitelist.Core
{
    internal enum JoinDecision
    {
        Allow,
        DenyBlacklisted,
        DenyNotWhitelisted
    }

    /// <summary>
    /// Owns the four lists (permanent/temporary × white/black) and decides who
    /// may join the host's lobby. Permanent lists live in JSON files under
    /// BepInEx/config/MyAwesomeWhitelist/; temporary lists die when a new lobby
    /// is hosted (<c>ResetSession</c>). The join check runs on network worker
    /// threads, so mutations swap a whole immutable snapshot (volatile read).
    /// </summary>
    internal sealed class ListService
    {
        internal const string WhiteFile = "whitelist.json";
        internal const string BlackFile = "blacklist.json";

        public static ListService Instance { get; private set; }

        public readonly NameList PermWhite = new NameList();
        public readonly NameList TempWhite = new NameList();
        public readonly NameList PermBlack = new NameList();
        public readonly NameList TempBlack = new NameList();

        private volatile WhitelistSnapshot _snapshot = WhitelistSnapshot.Empty;

        private ListService()
        {
        }

        public static void Init()
        {
            Instance = new ListService();
            Instance.ReloadFromDisk();
        }

        public WhitelistSnapshot Snapshot
        {
            get { return _snapshot; }
        }

        /// <summary>Reload the permanent lists from disk and rebuild the snapshot.</summary>
        public void ReloadFromDisk()
        {
            var permWhite = ListStore.Load(WhiteFile);
            CopyInto(permWhite, PermWhite);
            var permBlack = ListStore.Load(BlackFile);
            CopyInto(permBlack, PermBlack);
            RebuildSnapshot();
        }

        public void SaveWhitelist()
        {
            ListStore.Save(WhiteFile, PermWhite);
        }

        public void SaveBlacklist()
        {
            ListStore.Save(BlackFile, PermBlack);
        }

        /// <summary>New lobby ⇒ temporary lists and the session tracker die.
        /// Also lifts every leftover P2P ban — a fresh lobby must start with a
        /// clean kickedUsers, otherwise players blacklisted in an earlier
        /// lobby stay locked out of this one.</summary>
        public void ResetSession()
        {
            TempWhite.Clear();
            TempBlack.Clear();
            RebuildSnapshot();
            ClearKickedUsers();
            Plugin.Logger.LogInfo("Session lists cleared (new lobby)");
        }

        /// <summary>Feed the permanent blacklist into NetGame.kickedUsers so the
        /// Steam P2P layer (OnSessionRequest) refuses those players outright,
        /// and remove entries whose player is no longer blacklisted (deleted
        /// from the list, or the blacklist toggle was switched off). Vanilla
        /// never removes anything from that list, so we own its lifecycle.</summary>
        public void SyncKickedUsers(bool blacklistEnabled = true)
        {
            if (Multiplayer.NetGame.instance == null || Multiplayer.NetGame.kickedUsers == null)
            {
                return;
            }
            var kicked = Multiplayer.NetGame.kickedUsers;

            // Index the SteamIDs that are (still) supposed to be banned.
            var wanted = new HashSet<ulong>();
            if (blacklistEnabled)
            {
                var entries = PermBlack.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    ulong id;
                    if (!ulong.TryParse(entries[i].SteamId, out id))
                    {
                        Plugin.Logger.LogWarning($"Permanent blacklist entry '{entries[i].SteamId}' is not a valid SteamID, skipped");
                        continue;
                    }
                    wanted.Add(id);
                }
            }

            int added = 0, removed = 0;
            // Drop stale bans first (freed players may rejoin immediately).
            for (int i = kicked.Count - 1; i >= 0; i--)
            {
                if (!(kicked[i] is Steamworks.CSteamID))
                {
                    continue; // not ours to judge — some other connection type
                }
                ulong id = ((Steamworks.CSteamID)kicked[i]).m_SteamID;
                if (!wanted.Contains(id))
                {
                    kicked.RemoveAt(i);
                    removed++;
                }
            }
            // Then add any missing bans.
            foreach (ulong id in wanted)
            {
                var csid = new Steamworks.CSteamID(id);
                if (!kicked.Contains(csid))
                {
                    kicked.Add(csid);
                    added++;
                }
            }
            if (added > 0 || removed > 0)
            {
                Plugin.Logger.LogInfo($"NetGame.kickedUsers synced: +{added} / -{removed} (blacklist {(blacklistEnabled ? "on" : "off")})");
            }
        }

        /// <summary>Wipe every SteamID from kickedUsers (new lobby).</summary>
        public void ClearKickedUsers()
        {
            if (Multiplayer.NetGame.kickedUsers == null)
            {
                return;
            }
            var kicked = Multiplayer.NetGame.kickedUsers;
            for (int i = kicked.Count - 1; i >= 0; i--)
            {
                if (kicked[i] is Steamworks.CSteamID)
                {
                    kicked.RemoveAt(i);
                }
            }
        }

        /// <summary>Black beats white: a blacklisted player is denied even when
        /// whitelisted; with the whitelist on, anyone not on it is denied.</summary>
        public JoinDecision CheckJoin(string steamId, bool whitelistEnabled, bool blacklistEnabled, WhitelistSnapshot snapshot)
        {
            if (blacklistEnabled && (snapshot.PermBlack.Contains(steamId) || snapshot.TempBlack.Contains(steamId)))
            {
                return JoinDecision.DenyBlacklisted;
            }
            if (whitelistEnabled && !(snapshot.PermWhite.Contains(steamId) || snapshot.TempWhite.Contains(steamId)))
            {
                return JoinDecision.DenyNotWhitelisted;
            }
            return JoinDecision.Allow;
        }

        public void RebuildSnapshot()
        {
            _snapshot = new WhitelistSnapshot(
                CloneIndex(PermWhite), CloneIndex(TempWhite),
                CloneIndex(PermBlack), CloneIndex(TempBlack));
        }

        private static HashSet<string> CloneIndex(NameList list)
        {
            var set = new HashSet<string>();
            var entries = list.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                set.Add(entries[i].SteamId);
            }
            return set;
        }

        private static void CopyInto(NameList src, NameList dst)
        {
            dst.Clear();
            var entries = src.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                dst.Add(entries[i]);
            }
        }
    }

    /// <summary>Immutable membership snapshot safe to read from network threads.</summary>
    internal sealed class WhitelistSnapshot
    {
        public static readonly WhitelistSnapshot Empty = new WhitelistSnapshot(
            new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>());

        public readonly HashSet<string> PermWhite;
        public readonly HashSet<string> TempWhite;
        public readonly HashSet<string> PermBlack;
        public readonly HashSet<string> TempBlack;

        public WhitelistSnapshot(HashSet<string> permWhite, HashSet<string> tempWhite,
            HashSet<string> permBlack, HashSet<string> tempBlack)
        {
            PermWhite = permWhite;
            TempWhite = tempWhite;
            PermBlack = permBlack;
            TempBlack = tempBlack;
        }
    }
}
