using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using MyAwesomeWhitelist.Config;
using MyAwesomeWhitelist.Core;
using MyAwesomeWhitelist.I18n;
using MyAwesomeWhitelist.Patches;
using MyAwesomeWhitelist.UI;
using UnityEngine;

namespace MyAwesomeWhitelist
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        internal static new PluginConfig Config;

        private void Awake()
        {
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} v{PluginInfo.PLUGIN_VERSION} is loaded!");

            // Keep all plugin data in one folder: BepInEx/config/<PluginName>/
            // (whitelist.json, blacklist.json, names.json, lang/ already live
            // there — the .cfg joins them instead of sitting loose in config/).
            string cfgDir = Path.Combine(Paths.ConfigPath, PluginInfo.PLUGIN_NAME);
            string cfgPath = Path.Combine(cfgDir, PluginInfo.PLUGIN_NAME + ".cfg");
            var cfgFile = new ConfigFile(cfgPath, false);
            Config = new PluginConfig(cfgFile);
            ListService.Init();
            SessionTracker.Init();
            Core.NameCache.Init();
            LocalizationService.Init();  // generates lang/ files on first run

            PatchModule.Apply();

            // Re-sync the P2P ban list whenever the blacklist toggle flips
            // (plain event subscription, not a Harmony patch).
            Config.BlacklistEnabled.SettingChanged += NetGamePatches.OnBlacklistToggleChanged;

            var uiGo = new GameObject("MyAwesomeWhitelist.Window");
            Object.DontDestroyOnLoad(uiGo);
            uiGo.AddComponent<WhitelistWindow>();
        }
    }
}
