// Plugin.cs — BepInEx 플러그인의 진입점(Entry Point).
//
// [BepInEx 플러그인 구조 개요]
// BepInEx는 게임 실행 시 Unity의 MonoBehaviour 생명주기에 끼어들어
// 이 클래스의 Awake()를 자동으로 호출한다.
// 유니티 에디터의 Start/Awake와 동일한 개념이지만, 게임 씬이 로드되기 전에 실행된다.
//
// [HarmonyLib 패치 방식]
// Harmony는 런타임에 타겟 메서드의 IL 코드 앞/뒤에 우리 코드를 삽입한다.
// - Prefix  : 원본 메서드 실행 전에 호출 (실행 자체를 막을 수도 있음)
// - Postfix : 원본 메서드 실행 후에 호출 (반환값·상태를 읽거나 수정)
// - Transpiler : 원본 메서드의 IL 자체를 변경 (고급)
//
// 이 프로젝트는 Postfix만 사용한다.

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RayelleBX.Helpers;
using RayelleBX.Patches;
using System;
using RayelleBX.Config;

namespace RayelleBX;

// [BepInPlugin] 어트리뷰트는 BepInEx에게 이 클래스가 플러그인임을 알린다.
// 인자: (GUID, 표시 이름, 버전) — MyPluginInfo에 상수로 분리되어 있다.
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    // 다른 클래스에서도 로그를 출력할 수 있도록 internal static으로 공유한다.
    // BepInEx 로그는 BepInEx/LogOutput.log 파일과 게임 콘솔에 동시에 기록된다.
    internal static ManualLogSource Log;

    // Harmony 인스턴스. 나중에 패치를 일괄 해제(UnpatchSelf)할 때도 이 인스턴스를 사용한다.
    private Harmony _harmony;

    // Awake() : 플러그인 DLL이 로드된 직후 BepInEx가 호출하는 초기화 메서드.
    // 유니티의 MonoBehaviour.Awake()와 동일하다.
    private void Awake()
    {
        Log = base.Logger;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        // setting.cfg를 읽어 각 기능의 활성화 여부를 결정한다
        PluginConfig.Load();

        // Harmony 인스턴스를 GUID로 생성. 같은 GUID로 여러 번 패치하면 중복 적용되므로 주의.
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        // --- 패치 등록 ---
        // PatchAll은 대상 메서드를 찾지 못하면 예외를 던질 수 있다.
        // 각각 try-catch로 보호해 한 곳에서 실패해도 나머지 패치는 계속 등록된다.
        if (PluginConfig.QuickMenuMacro)
            TryPatchAll(_harmony, typeof(QuickMenuUIEnablePatch));

        if (PluginConfig.CharRecoveryMacro)
            TryPatchAll(_harmony, typeof(CharRecoveryUIEnablePatch));

        if (PluginConfig.CharCostumeLogging)
        {
            CostumeConfig.EnsureFile();
            CharUIEnablePatch.ApplyPatches(_harmony);
            DeckMessagePatch.ApplyPatches(_harmony);
        }

        if (PluginConfig.InfiniteGachaMacro)
            GachaMacroUIEnablePatch.ApplyPatches(_harmony);

        Log.LogInfo("Harmony Patch Complete");

        DiagnosticHelper.RunStartupDiagnostics();
    }

    /// <summary>
    /// PatchAll을 try-catch로 감싼다.
    /// 대상 메서드를 찾지 못하거나 패치 중 예외가 발생해도 나머지 패치 등록이 계속된다.
    /// </summary>
    private static void TryPatchAll(Harmony harmony, Type patchClass)
    {
        try
        {
            harmony.PatchAll(patchClass);
        }
        catch (Exception e)
        {
            Log.LogError($"[Plugin] PatchAll 실패: {patchClass.Name}\n{e}");
        }
    }
}
