using System.Collections.Generic;
using Multiplayer;

namespace MyAwesomeWhitelist.Core
{
    /// <summary>
    /// Players currently connected to this lobby, keyed by SteamID. Populated
    /// from the OnClientHelo postfix (where the nickname becomes known) and the
    /// AddPlayer path, cleared per-connection on OnDisconnect. Only touched from
    /// patches that may run on network threads, so mutations are lock-guarded;
    /// the UI reads a cloned snapshot each frame.
    /// </summary>
    internal sealed class SessionTracker
    {
        public sealed class PlayerInfo
        {
            public string SteamId;
            public string Name;
            public uint HostId;
        }

        private readonly object _lock = new object();
        private readonly Dictionary<string, PlayerInfo> _bySteamId = new Dictionary<string, PlayerInfo>();

        public static SessionTracker Instance { get; private set; }

        private SessionTracker()
        {
        }

        public static void Init()
        {
            Instance = new SessionTracker();
        }

        public void Remember(string steamId, string name, uint hostId)
        {
            if (string.IsNullOrEmpty(steamId))
            {
                return;
            }
            lock (_lock)
            {
                PlayerInfo info;
                if (!_bySteamId.TryGetValue(steamId, out info))
                {
                    info = new PlayerInfo { SteamId = steamId };
                    _bySteamId[steamId] = info;
                }
                info.HostId = hostId;
                if (!string.IsNullOrEmpty(name) && name != NetMsgId.AddPlayer.ToString())
                {
                    info.Name = name;
                }
            }
        }

        public void Forget(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
            {
                return;
            }
            lock (_lock)
            {
                _bySteamId.Remove(steamId);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _bySteamId.Clear();
            }
        }

        /// <summary>Resolve the lobby SteamID for a connection object (a boxed
        /// CSteamID on the Steam transport).</summary>
        public static string SteamIdOf(object connection)
        {
            if (connection is Steamworks.CSteamID)
            {
                return ((Steamworks.CSteamID)connection).ToString();
            }
            return connection == null ? null : connection.ToString();
        }

        public List<PlayerInfo> Snapshot()
        {
            lock (_lock)
            {
                return new List<PlayerInfo>(_bySteamId.Values);
            }
        }
    }
}
