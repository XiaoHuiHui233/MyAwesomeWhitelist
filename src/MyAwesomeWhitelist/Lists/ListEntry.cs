using System;

namespace MyAwesomeWhitelist.Lists
{
    /// <summary>
    /// One record in a whitelist/blacklist: the player's SteamID (64-bit, digits
    /// only), their display name at the time they were added (informational), and
    /// when they were added. Immutable — removal drops the whole entry.
    /// </summary>
    internal sealed class ListEntry
    {
        public readonly string SteamId;
        public readonly string Name;
        public readonly string AddedAt;

        public ListEntry(string steamId, string name, string addedAt)
        {
            SteamId = steamId;
            Name = name ?? string.Empty;
            AddedAt = addedAt ?? string.Empty;
        }

        public static ListEntry Create(string steamId, string name)
        {
            return new ListEntry(steamId, name, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? SteamId : $"{Name} ({SteamId})";
        }
    }
}
