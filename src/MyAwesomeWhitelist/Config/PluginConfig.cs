using BepInEx.Configuration;

namespace MyAwesomeWhitelist.Config
{
    /// <summary>
    /// BepInEx .cfg bindings. The window hotkey is a combo spec string
    /// ("Ctrl+Shift+W") parsed by <see cref="UI.Hotkey"/> — "Ctrl" matches both
    /// Ctrl and Cmd so Windows and macOS users share one default.
    /// </summary>
    internal sealed class PluginConfig
    {
        public readonly ConfigEntry<string> ToggleWindowHotkey;
        public readonly ConfigEntry<bool> WhitelistEnabled;
        public readonly ConfigEntry<bool> BlacklistEnabled;
        public readonly ConfigEntry<string> LanguageCode;

        public PluginConfig(ConfigFile cfg)
        {
            ToggleWindowHotkey = cfg.Bind("General", "ToggleWindowHotkey", "Ctrl+Shift+W",
                "Hotkey to open/close the manager window. Modifiers (Ctrl/Cmd, Shift, Alt) joined with '+', main key last, e.g. Ctrl+Shift+W or Alt+F6.");
            WhitelistEnabled = cfg.Bind("Whitelist", "Enabled", false,
                "When enabled (and you are the host), only whitelisted players may join.");
            BlacklistEnabled = cfg.Bind("Blacklist", "Enabled", true,
                "When enabled (and you are the host), blacklisted players are refused on join and force-kicked.");
            LanguageCode = cfg.Bind("General", "Language", "auto",
                "UI language: auto, zh-cn, en, or jp. Auto-detects from system on first launch.");
        }
    }
}
