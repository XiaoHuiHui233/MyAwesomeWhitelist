using HarmonyLib;
using MyAwesomeWhitelist.Core;
using Multiplayer;

namespace MyAwesomeWhitelist.Patches
{
    /// <summary>
    /// All Harmony patches on the game's netcode. The join checks (P2/P3) may
    /// run on network worker threads (the host defaults to serverThreads=2), so
    /// they only consult an immutable list snapshot and marshal every kick over
    /// to the main thread via <see cref="KickService"/>.
    /// </summary>
    [HarmonyPatch]
    internal static class NetGamePatches
    {
        // P1: a fresh lobby kills the temporary lists and lifts every leftover
        // P2P ban from earlier lobbies (vanilla never clears kickedUsers).
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetGame), nameof(NetGame.HostGame))]
        private static void HostGame_Prefix()
        {
            if (ListService.Instance != null)
            {
                ListService.Instance.ResetSession();
            }
            if (SessionTracker.Instance != null)
            {
                SessionTracker.Instance.Clear();
            }
        }

        // P2: first gate — a new client connected. Deny before the Helo handshake
        // is sent, so rejected players never even reach the lobby.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetGame), nameof(NetGame.OnServerConnect))]
        private static bool OnServerConnect_Prefix(NetHost client)
        {
            var lists = ListService.Instance;
            var cfg = Plugin.Config;
            if (lists == null || cfg == null || client == null || !NetGame.isServer)
            {
                return true;
            }
            string steamId = SessionTracker.SteamIdOf(client.connection);
            if (string.IsNullOrEmpty(steamId))
            {
                return true;
            }
            JoinDecision decision = lists.CheckJoin(steamId,
                cfg.WhitelistEnabled.Value, cfg.BlacklistEnabled.Value, lists.Snapshot);
            if (decision == JoinDecision.Allow)
            {
                return true;
            }
            string reason = decision == JoinDecision.DenyBlacklisted ? "blacklisted" : "not whitelisted";
            Plugin.Logger.LogInfo($"Refusing connection from {steamId}: {reason}");
            // Runs NetGame.Kick + OnDisconnect on the main thread; kickedUsers is
            // filled first so OnSessionRequest rejects any immediate reconnect.
            // Both rejections are transient (non-ban) — the player can rejoin
            // once the host whitelist/blacklist lets them.
            KickService.EnqueueForceKick(client, reason);
            return false; // skip original: no Helo handshake for rejected clients
        }

        // P3: second gate — the client requested to spawn its player. Catches the
        // "blacklisted while already connected" case. Never reads the stream (the
        // original must still be able to parse it on the Allow path — on Deny the
        // original is skipped anyway).
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetGame), "OnRequestAddPlayerServer")]
        private static bool OnRequestAddPlayerServer_Prefix(NetHost client)
        {
            var lists = ListService.Instance;
            var cfg = Plugin.Config;
            if (lists == null || cfg == null || client == null || !NetGame.isServer)
            {
                return true;
            }
            string steamId = SessionTracker.SteamIdOf(client.connection);
            if (string.IsNullOrEmpty(steamId))
            {
                return true;
            }
            JoinDecision decision = lists.CheckJoin(steamId,
                cfg.WhitelistEnabled.Value, cfg.BlacklistEnabled.Value, lists.Snapshot);
            if (decision == JoinDecision.Allow)
            {
                return true;
            }
            string reason = decision == JoinDecision.DenyBlacklisted ? "blacklisted (late)" : "not whitelisted (late)";
            Plugin.Logger.LogInfo($"Blocking player spawn for {steamId}: {reason}");
            // Blacklist denial keeps the P2P ban; whitelist denial must not —
            // the player should get back in once the host changes the rules.
            if (decision == JoinDecision.DenyBlacklisted)
            {
                KickService.EnqueueBanKickBySteamId(steamId, reason);
            }
            else
            {
                KickService.EnqueueForceKick(client, reason);
            }
            return false;
        }

        // P4: nickname becomes known here — remember the player for the UI
        // and update the persistent name cache.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetGame), nameof(NetGame.OnClientHelo))]
        private static void OnClientHelo_Postfix(NetHost client)
        {
            if (client == null || !NetGame.isServer)
            {
                return;
            }
            string steamId = SessionTracker.SteamIdOf(client.connection);
            SessionTracker.Instance?.Remember(steamId, client.name, client.hostId);
            Core.NameCache.Record(steamId, client.name);  // persist name mapping
        }

        // P5: forget a departing player.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetGame), nameof(NetGame.OnDisconnect))]
        private static void OnDisconnect_Postfix(object connection)
        {
            if (!NetGame.isServer)
            {
                return;
            }
            SessionTracker.Instance?.Forget(SessionTracker.SteamIdOf(connection));
        }

        // P6: NetGame spins up per session — push the permanent blacklist into
        // NetGame.kickedUsers so OnSessionRequest refuses those players at the
        // Steam P2P layer from the very first packet, and drop bans that no
        // longer match the list/toggle state.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetGame), "Awake")]
        private static void NetGame_Awake_Postfix()
        {
            var cfg = Plugin.Config;
            ListService.Instance?.SyncKickedUsers(cfg == null || cfg.BlacklistEnabled.Value);
        }

        // Toggle changes arrive as a plain .NET event (no Harmony needed —
        // patching an event accessor would throw and abort the whole PatchAll,
        // which once took every other patch down with it).
        // ConfigEntry<T>.SettingChanged is a plain EventHandler (no args).
        public static void OnBlacklistToggleChanged(object sender, System.EventArgs _)
        {
            var cfg = Plugin.Config;
            if (cfg == null || ListService.Instance == null || !Multiplayer.NetGame.isServer)
            {
                return;
            }
            // Turning the blacklist off must un-ban everyone it had banned;
            // turning it on re-bans the listed players.
            ListService.Instance.SyncKickedUsers(cfg.BlacklistEnabled.Value);
        }
    }
}
