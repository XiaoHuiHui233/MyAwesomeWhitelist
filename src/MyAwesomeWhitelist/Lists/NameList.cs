using System.Collections.Generic;

namespace MyAwesomeWhitelist.Lists
{
    /// <summary>
    /// An ordered list of <see cref="ListEntry"/> with a hash index for O(1)
    /// membership checks. Entries are unique by SteamID. Not thread-safe by
    /// itself — <see cref="Core.ListService"/> swaps whole snapshots for the
    /// network-thread readers.
    /// </summary>
    internal sealed class NameList
    {
        private readonly List<ListEntry> _entries = new List<ListEntry>();
        private readonly HashSet<string> _index = new HashSet<string>();

        public IList<ListEntry> Entries
        {
            get { return _entries; }
        }

        public int Count
        {
            get { return _entries.Count; }
        }

        public bool Contains(string steamId)
        {
            return steamId != null && _index.Contains(steamId);
        }

        /// <summary>Adds the entry if its SteamID is not already present.</summary>
        public bool Add(ListEntry entry)
        {
            if (entry == null || Contains(entry.SteamId))
            {
                return false;
            }
            _entries.Add(entry);
            _index.Add(entry.SteamId);
            return true;
        }

        public bool Remove(string steamId)
        {
            if (!Contains(steamId))
            {
                return false;
            }
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].SteamId == steamId)
                {
                    _entries.RemoveAt(i);
                    break;
                }
            }
            _index.Remove(steamId);
            return true;
        }

        public void Clear()
        {
            _entries.Clear();
            _index.Clear();
        }

        /// <summary>Updates the display name for a known SteamID (used when we
        /// learn a player's real nickname after they joined).</summary>
        public void UpdateName(string steamId, string name)
        {
            if (steamId == null || name == null)
            {
                return;
            }
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].SteamId == steamId && _entries[i].Name != name)
                {
                    _entries[i] = new ListEntry(steamId, name, _entries[i].AddedAt);
                    return;
                }
            }
        }
    }
}
