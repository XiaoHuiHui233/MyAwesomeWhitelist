using System;
using HarmonyLib;

namespace MyAwesomeWhitelist.Patches
{
    internal static class PatchModule
    {
        public static void Apply()
        {
            try
            {
                var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
                harmony.PatchAll(typeof(NetGamePatches));
                Plugin.Logger.LogInfo("Harmony patches applied");
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Failed to apply Harmony patches: {e}");
            }
        }
    }
}
