// CharRecoveryUIEnablePatch.cs — 회복 탭에 자동 먹이기 매크로 버튼을 추가하는 패치.
//
// [패치 대상]
// CharRecoveryUI.SetUI() — 캐릭터 UI의 회복 탭이 열릴 때 호출된다.
//
// [동작 흐름]
// 1. 기존 "Button - Auto" 옆에 "Button - Macro"("자동 먹이기") 버튼을 복제 생성
// 2. 부모 오브젝트에 HorizontalLayoutGroup을 추가해 두 버튼을 나란히 배치
// 3. 버튼 클릭 시 EatMacroLoop 코루틴 시작
//
// [EatMacroLoop 자동화 순서]
//   뒤로가기 → 연결(Connect) 버튼 → 코스튬1 선택 → 코스튬 연결 활성화
//   → 연결 → 코스튬0 선택 → 코스튬 연결 활성화 → 회복 버튼 2회
//   → 먹이기 아이템 N회 클릭(GetFeedCount로 계산) → 먹기(Eat) 버튼
//
// [GetFeedCount]
// "Text - TotalHealth"(최대 HP)와 "Text - Health"(현재 HP) TMP 텍스트를 파싱해
// (최대 - 현재) / 2 = 먹이기 아이템 클릭 횟수를 산출한다.
// 아이템 1개당 HP 2 회복을 전제로 한다.
//
// [Q키 또는 오버레이 종료 버튼으로 중단]

using HarmonyLib;
using RayelleBX.Helpers;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RayelleBX.Patches;

[HarmonyPatch(typeof(CharRecoveryUI), "SetUI")]
public class CharRecoveryUIEnablePatch
{
    // __instance : 패치된 CharRecoveryUI 인스턴스
    private static void Postfix(CharRecoveryUI __instance)
    {
        Plugin.Log.LogInfo("CharRecoveryUI Patch Activated");

        // 복제 원본으로 쓸 Auto 버튼 — 없으면 UI 구조가 바뀐 것이므로 종료
        GameObject recoveryButton = UIHelper.FindOrLog(
            "Singleton (DontDestroy)/AppManager/UI/CharUI(Clone)/UIRoot/Mask/Tab - 4 - Recovery/Object - Button/Button - Auto",
            "recoveryButton");
        if (recoveryButton == null) return;

        Transform parent = recoveryButton.transform.parent;
        // 이미 매크로 버튼이 있으면 중복 생성 방지
        if (MacroButtonAlreadyExists(parent)) return;

        SetupLayoutGroup(parent);
        CreateMacroButton(recoveryButton, __instance);
    }

    private static bool MacroButtonAlreadyExists(Transform parent)
    {
        return parent.Find("Button - Macro") != null;
    }

    private static void SetupLayoutGroup(Transform parent)
    {
        // Auto 버튼과 Macro 버튼을 가로로 나란히 배치하기 위해 HorizontalLayoutGroup 추가
        // 이미 있으면 재사용, 없으면 추가
        HorizontalLayoutGroup hlg = parent.GetComponent<HorizontalLayoutGroup>()
            ?? parent.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
    }

    private static void CreateMacroButton(GameObject original, CharRecoveryUI instance)
    {
        // Auto 버튼을 복제 — 크기·스타일을 그대로 상속받는다
        GameObject buttonObj = Object.Instantiate(original, original.transform.parent);
        buttonObj.name = "Button - Macro";
        SetButtonLabel(buttonObj, "자동 먹이기");
        BindMacroButtonClick(buttonObj, instance);
    }

    private static void SetButtonLabel(GameObject buttonObj, string text)
    {
        // "Text - Title" 하위 TMP 텍스트를 교체해 버튼 라벨을 변경한다
        Transform t = buttonObj.transform.Find("Text - Title");
        if (t == null) return;
        TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }

    private static void BindMacroButtonClick(GameObject buttonObj, CharRecoveryUI instance)
    {
        Button btn = buttonObj.GetComponent<Button>();
        if (btn == null)
        {
            Plugin.Log.LogInfo("MacroMenu Button component not found");
            return;
        }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener((UnityAction)(() =>
        {
            Plugin.Log.LogInfo("MacroMenu Button Clicked");
            StartMacro(instance);
        }));
    }

    private static void StartMacro(CharRecoveryUI instance)
    {
        if (ComponentHelper.IsMacroRunning)
        {
            Plugin.Log.LogInfo("Macro already running");
            return;
        }
        CoroutineHelper.GetOrCreateRunner().StartCoroutine(EatMacroLoop());
    }

    /// <summary>
    /// 자동 먹이기 코루틴.
    /// UI 버튼을 순서대로 클릭한다. 각 단계가 실패하면 break로 루프를 종료한다.
    /// IsMacroRunning이 false가 되면 다음 사이클 시작 전에 종료된다.
    /// </summary>
    private static IEnumerator EatMacroLoop()
    {
        ComponentHelper.IsMacroRunning = true;
        while (ComponentHelper.IsMacroRunning && StepClickBack())
        {
            yield return new WaitForSeconds(0.5f);
            if (!StepClickConnect()) break;
            yield return new WaitForSeconds(0.5f);
            // 코스튬1 → 연결 활성화 → 코스튬0 → 연결 활성화 순으로 두 코스튬 연결
            if (!StepClickCostumeItem1()) break;
            yield return new WaitForSeconds(0.5f);
            if (!StepClickCostumeConnectEnable()) break;
            yield return new WaitForSeconds(0.5f);
            if (!StepClickConnect()) break;
            yield return new WaitForSeconds(0.5f);
            if (!StepClickCostumeItem0()) break;
            yield return new WaitForSeconds(0.5f);
            if (!StepClickCostumeConnectEnable()) break;
            yield return new WaitForSeconds(0.5f);
            // 회복 버튼을 2회 클릭해 회복 탭으로 이동
            if (!StepClickRecovery()) break;
            yield return new WaitForSeconds(0.5f);
            if (!StepClickRecovery()) break;
            yield return new WaitForSeconds(0.5f);

            // 현재 HP 부족분만큼 먹이기 아이템 클릭 (아이템 1개 = HP +2)
            int feedCount = GetFeedCount();
            for (int i = 0; i < feedCount && StepClickFeedItem(); i++)
                yield return new WaitForSeconds(0.13f);

            if (!StepClickEat()) break;
            yield return new WaitForSeconds(0.5f);
        }
        ComponentHelper.IsMacroRunning = false;
    }

    /// <summary>
    /// HP 텍스트를 파싱해 필요한 먹이기 아이템 클릭 횟수를 계산한다.
    /// Text - TotalHealth : "/{최대HP}" 형식
    /// Text - Health      : "{현재HP}" 형식
    /// 반환값 = floor((최대HP - 현재HP) / 2)
    /// </summary>
    private static int GetFeedCount()
    {
        GameObject totalGo = GameObject.Find(
            "Singleton (DontDestroy)/AppManager/UI/CharUI(Clone)/UIRoot/Mask/Tab - 4 - Recovery/UseLayout/UseChar/Green/Text - TotalHealth");
        GameObject curGo = GameObject.Find(
            "Singleton (DontDestroy)/AppManager/UI/CharUI(Clone)/UIRoot/Mask/Tab - 4 - Recovery/UseLayout/UseChar/Green/Text - TotalHealth/Text - Health");

        if (totalGo == null || curGo == null)
        {
            Plugin.Log.LogInfo("Health GameObjects not found");
            return 0;
        }

        TextMeshProUGUI totalTmp = totalGo.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI curTmp   = curGo.GetComponent<TextMeshProUGUI>();
        if (totalTmp == null || curTmp == null)
        {
            Plugin.Log.LogInfo("TextMeshProUGUI components not found");
            return 0;
        }

        // TrimStart('/') : "/{최대HP}" 형식에서 '/'를 제거
        if (!int.TryParse(totalTmp.text.TrimStart('/').Trim(), out int total))
        {
            Plugin.Log.LogInfo("Failed to parse total health: " + totalTmp.text);
            return 0;
        }
        if (!int.TryParse(curTmp.text.Trim(), out int current))
        {
            Plugin.Log.LogInfo("Failed to parse current health: " + curTmp.text);
            return 0;
        }

        int feedCount = Mathf.FloorToInt((total - current) / 2f);
        Plugin.Log.LogInfo($"Total: {total}, Current: {current}, FeedCount: {feedCount}");
        return feedCount;
    }

    // --- 매크로 단계별 클릭 메서드 ---
    // 실패(버튼 없음) 시 false를 반환해 EatMacroLoop에서 break로 중단한다.

    private static bool StepClickFeedItem() =>
        UIHelper.TryInvokeButton(
            "Singleton (DontDestroy)/AppManager/UI/CharUI(Clone)/UIRoot/Mask/Tab - 4 - Recovery/LoopScroll/Viewport/Content/PoolItem_0 (CharRecoveryLoopScrollItem)",
            "feedItem");

    private static bool StepClickBack() =>
        UIHelper.TryInvokeButton(
            "Singleton (DontDestroy)/AppManager/UI/CharUI(Clone)/UIRoot/Mask/BottomObject/Button - Back",
            "backButton");

    private static bool StepClickConnect() =>
        UIHelper.TryInvokeButton(
            "Singleton (DontDestroy)/AppManager/UI/CharUI(Clone)/UIRoot/Mask/Tab - 1 - CharInfo/ConnectionStats/Object - Connect/Button - Connect",
            "connectButton");

    private static bool StepClickCostumeItem1() =>
        UIHelper.TryInvokeButton(
            "Singleton (DontDestroy)/AppManager/UI/CostumeConnectPopupUI(Clone)/Button - background/Parent/Image - Backgrond/CostumeScrollView/Viewport/Content/CostumeConnectScrollItem1",
            "connectCostumeButton1");

    private static bool StepClickCostumeItem0() =>
        UIHelper.TryInvokeButton(
            "Singleton (DontDestroy)/AppManager/UI/CostumeConnectPopupUI(Clone)/Button - background/Parent/Image - Backgrond/CostumeScrollView/Viewport/Content/CostumeConnectScrollItem0",
            "connectCostumeButton0");

    private static bool StepClickCostumeConnectEnable() =>
        UIHelper.TryInvokeButton(
            "Singleton (DontDestroy)/AppManager/UI/CostumeConnectPopupUI(Clone)/Button - background/Parent/Image - Backgrond/CostumeInfo/Button - CostumeConnect /Button - CostumeConnect - Enable",
            "costumeConnectEnable");

    private static bool StepClickRecovery() =>
        UIHelper.TryInvokeButton(
            "Singleton (DontDestroy)/AppManager/UI/CharUI(Clone)/UIRoot/Mask/LayoutGroup/ButtonParent/Button - Recovery",
            "recoveryButton");

    private static bool StepClickEat() =>
        UIHelper.TryInvokeButton(
            "Singleton (DontDestroy)/AppManager/UI/CharUI(Clone)/UIRoot/Mask/Tab - 4 - Recovery/Object - Button/Button - Recovery",
            "eatButton");
}
