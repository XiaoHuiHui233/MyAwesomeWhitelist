using System;
using System.Collections.Concurrent;
using Multiplayer;

namespace MyAwesomeWhitelist.Core
{
    /// <summary>
    /// Force-kick that cannot be blocked by client-side "anti-kick" mods (such
    /// as HFF-Changed, which only swallows the NetMsgId.Kick message hundreds of
    /// times). The host performs two server-side steps: NetGame.Kick (records
    /// the SteamID in NetGame.kickedUsers so the Steam P2P layer refuses any
    /// reconnect) followed by NetGame.OnDisconnect (unilaterally destroys every
    /// object of that client and broadcasts RemoveHost — nothing the client
    /// does can prevent this).
    ///
    /// <b>kickedUsers is forever in vanilla</b> — once a SteamID lands in that
    /// static list, OnSessionRequest closes the P2P session on every future
    /// join attempt until the host restarts the game. That is only wanted for
    /// blacklist bans; a plain kick or a whitelist rejection must let the
    /// player rejoin, so <see cref="ForceKick"/> scrubs the entry again right
    /// after kicking unless <paramref name="banReconnect"/> is set.
    ///
    /// Patches may fire on network worker threads; anything touching Unity
    /// objects goes through <see cref="RunOnMainThread"/>, drained each frame by
    /// the UI component on the main thread.
    /// </summary>
    internal static class KickService
    {
        private static readonly ConcurrentQueue<Action> MainThreadQueue = new ConcurrentQueue<Action>();

        public static void RunOnMainThread(Action action)
        {
            if (action != null)
            {
                MainThreadQueue.Enqueue(action);
            }
        }

        /// <summary>Called every frame from a main-thread MonoBehaviour.</summary>
        public static void DrainMainThreadQueue()
        {
            Action action;
            while (MainThreadQueue.TryDequeue(out action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"Main-thread action failed: {e}");
                }
            }
        }

        /// <summary>
        /// Force-kick a connected client. Must run on the main thread (it
        /// destroys Unity objects); convenience overload enqueues it.
        /// </summary>
        public static void EnqueueForceKick(NetHost host, string reason)
        {
            if (host == null)
            {
                return;
            }
            string sid = SessionTracker.SteamIdOf(host.connection);
            RunOnMainThread(delegate
            {
                ForceKick(host, sid, reason);
            });
        }

        /// <summary>Force-kick by SteamID (resolves the live NetHost, if any).
        /// Outside a lobby nobody is connected — just a no-op (no ban, this is
        /// the transient-kick path).</summary>
        public static void EnqueueForceKickBySteamId(string steamId, string reason)
        {
            if (string.IsNullOrEmpty(steamId) || NetGame.instance == null)
            {
                return;
            }
            RunOnMainThread(delegate
            {
                NetHost host = FindHostBySteamId(steamId);
                if (host != null)
                {
                    ForceKick(host, steamId, reason);
                }
                else
                {
                    BanReconnect(steamId);
                }
            });
        }

        /// <summary>Force-kick + keep the P2P ban: blacklisted players are
        /// refused on reconnect too (kickedUsers entry stays). Outside a lobby
        /// only the ban part is possible — the kick resolves to nobody.</summary>
        public static void EnqueueBanKickBySteamId(string steamId, string reason)
        {
            if (string.IsNullOrEmpty(steamId) || NetGame.kickedUsers == null)
            {
                return;
            }
            RunOnMainThread(delegate
            {
                NetHost host = FindHostBySteamId(steamId);
                if (host != null)
                {
                    ForceKick(host, steamId, reason, banReconnect: true);
                }
                else
                {
                    BanReconnect(steamId);
                }
            });
        }

        /// <summary>Record the SteamID so a player who is not currently connected
        /// is refused at the Steam P2P layer when they try to (re)join.
        /// Safe outside a lobby: the static list survives, so the ban applies
        /// when the next lobby is hosted (NetGame.Awake re-syncs anyway).</summary>
        public static void BanReconnect(string steamId)
        {
            if (NetGame.kickedUsers == null)
            {
                return; // not in any network context — SyncKickedUsers picks it up on lobby start
            }
            ulong id;
            if (!ulong.TryParse(steamId, out id))
            {
                return;
            }
            var csid = new Steamworks.CSteamID(id);
            if (!NetGame.kickedUsers.Contains(csid))
            {
                NetGame.kickedUsers.Add(csid);
            }
        }

        /// <summary>Remove a SteamID from kickedUsers so the player can join
        /// again (used for non-blacklist kicks and when the blacklist is
        /// turned off / an entry is deleted).</summary>
        public static void UnbanReconnect(string steamId)
        {
            if (NetGame.kickedUsers == null)
            {
                return;
            }
            ulong id;
            if (!ulong.TryParse(steamId, out id))
            {
                return;
            }
            var csid = new Steamworks.CSteamID(id);
            for (int i = NetGame.kickedUsers.Count - 1; i >= 0; i--)
            {
                if (NetGame.kickedUsers[i] is Steamworks.CSteamID
                    && (Steamworks.CSteamID)NetGame.kickedUsers[i] == csid)
                {
                    NetGame.kickedUsers.RemoveAt(i);
                }
            }
        }

        private static NetHost FindHostBySteamId(string steamId)
        {
            var net = NetGame.instance;
            if (net == null)
            {
                return null;
            }
            var all = net.allclients;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && SessionTracker.SteamIdOf(all[i].connection) == steamId)
                {
                    return all[i];
                }
            }
            return null;
        }

        private static void ForceKick(NetHost host, string steamId, string reason, bool banReconnect = false)
        {
            var net = NetGame.instance;
            if (net == null || host == null)
            {
                return;
            }
            Plugin.Logger.LogInfo($"Force-kicking {host.name} ({steamId}): {reason ?? "manual"}");
            try
            {
                // 1) Record in kickedUsers first — OnSessionRequest then closes
                //    the P2P session on any immediate reconnect attempt.
                net.Kick(host);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"Kick({steamId}) threw: {e.Message}");
            }
            try
            {
                // 2) Unilateral server-side teardown. suppressMessage avoids the
                //    host seeing a "connection lost" dialog for it.
                net.OnDisconnect(host.connection, true);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"OnDisconnect({steamId}) threw: {e.Message}");
            }
            // 3) Vanilla never clears kickedUsers — without this a plain kick
            //    or whitelist rejection would ban the player until the host
            //    restarts the game. Only blacklist bans keep the entry.
            if (!banReconnect && steamId != null)
            {
                UnbanReconnect(steamId);
            }
        }
    }
}
