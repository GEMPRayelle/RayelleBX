using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RayelleBX.Patches;
using System.Reflection;

namespace RayelleBX;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    private Harmony _harmony;

    private void Awake()
    {
        Log = base.Logger;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        // OverwhelmIndicatorPatch: TargetMethods()로 탐색 후 수동 패치 (PatchAll은 이중 호출 발생)
        var postfix = new HarmonyMethod(typeof(OverwhelmIndicatorPatch), "Postfix");
        foreach (MethodBase method in OverwhelmIndicatorPatch.GetTargetMethods())
            _harmony.Patch(method, postfix: postfix);

        _harmony.PatchAll(typeof(GameFieldDefaultUIEnablePatch));
        _harmony.PatchAll(typeof(SymbolRemovePatch));

        Log.LogInfo("Harmony Patch Complete");
    }
}
