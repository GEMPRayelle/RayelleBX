// QuickMenuUIEnablePatch.cs — 퀵메뉴에 대화 자동 스킵 매크로 버튼을 추가하는 패치.
//
// [패치 대상]
// QuickMenuUI.SetMenu() — 퀵메뉴가 열릴 때마다 호출된다.
// 매번 호출되므로 버튼이 이미 생성되어 있는지 확인하고 중복 생성을 방지한다.
//
// [동작 흐름]
// 1. 기존 "object0"(대화 버튼)을 기준으로 "MacroMenu" 버튼을 복제 생성
// 2. 아이콘 UISprite를 보라색으로 틴트해 일반 버튼과 구별
// 3. 버튼 클릭 시 TalkMacroLoop 코루틴 시작
//    - 대화 버튼 클릭 → BalloonScriptUI의 Skip 버튼이 나타날 때까지 폴링 → Skip 클릭 반복
// 4. Q키 또는 오버레이 종료 버튼으로 중단
//
// [_listenerBound 플래그]
// SetMenu()는 퀵메뉴가 열릴 때마다 재호출된다.
// MacroMenu 버튼이 이미 있으면 리스너를 다시 등록하지 않는다.
// (RemoveAllListeners + AddListener로 중복 방지하므로 사실상 안전하지만 로그 노이즈 방지 목적)

using HarmonyLib;
using RayelleBX.Helpers;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RayelleBX.Patches;

[HarmonyPatch(typeof(QuickMenuUI), "SetMenu")]
public class QuickMenuUIEnablePatch
{
    // BalloonScriptUI의 Skip 버튼을 폴링하는 간격 (초)
    private const float SkipButtonPollInterval = 0.1f;
    // 스킵 클릭 후 다음 대화 사이클 시작까지 대기 시간 (초)
    private const float LoopInterval = 0.2f;
    // 리스너 중복 등록 방지 플래그 — SetMenu 재호출 시 이미 바인딩됐으면 skip
    private static bool _listenerBound;

    // __instance : 패치된 QuickMenuUI 인스턴스
    private static void Postfix(QuickMenuUI __instance)
    {
        Plugin.Log.LogInfo("QuickMenuUIEnablePatch Activated");

        // 복제 원본으로 쓸 대화 버튼 — 없으면 퀵메뉴 구조가 바뀐 것이므로 종료
        GameObject talkButton = UIHelper.FindOrLog(
            "Singleton (DontDestroy)/AppManager/UI/QuickMenuUI(Clone)/Parent/MenuButtonLayout/Layout - Menu/BottomMenus - Link/object0",
            "talkButton");
        if (talkButton == null) return;

        Transform macroMenu = talkButton.transform.parent.Find("MacroMenu");
        if (macroMenu != null)
        {
            // 이미 버튼이 존재하는 경우 — 리스너만 재바인딩 (이미 바인딩됐으면 skip)
            if (_listenerBound) return;
            BindMacroButtonClick(macroMenu.gameObject, __instance);
        }
        else
        {
            // 버튼이 없는 경우 — 새로 생성
            _listenerBound = false;
            CreateMacroButton(talkButton, __instance);
        }
    }

    private static void CreateMacroButton(GameObject original, QuickMenuUI instance)
    {
        // 대화 버튼을 복제해 같은 부모 아래에 배치 — 레이아웃·크기를 자동으로 상속
        GameObject buttonObj = Object.Instantiate(original, original.transform.parent);
        buttonObj.name = "MacroMenu";
        TintMenuIcon(buttonObj);
        BindMacroButtonClick(buttonObj, instance);
    }

    private static void TintMenuIcon(GameObject buttonObj)
    {
        // "Image - menuIcon" 하위 오브젝트의 UISprite 색상을 보라색으로 변경
        // 일반 버튼(흰색)과 시각적으로 구별하기 위함
        Transform t = buttonObj.transform.Find("Image - menuIcon");
        if (t == null)
        {
            Plugin.Log.LogInfo("Image - menuIcon not found");
            return;
        }
        UISprite sprite = t.GetComponent<UISprite>();
        if (sprite != null)
            sprite.color = new Color(0.5f, 0f, 1f, 1f);
        else
            Plugin.Log.LogInfo("UISprite not found on Image - menuIcon");
    }

    private static void BindMacroButtonClick(GameObject buttonObj, QuickMenuUI instance)
    {
        Button btn = buttonObj.GetComponent<Button>();
        if (btn == null) return;

        // RemoveAllListeners : SetMenu 재호출 시 이전 리스너가 중복 등록되는 것을 방지
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener((UnityAction)(() =>
        {
            Plugin.Log.LogInfo("MacroMenu Button Clicked");
            StartMacro(instance);
        }));
        _listenerBound = true;
    }

    private static void StartMacro(QuickMenuUI instance)
    {
        // 이미 매크로가 실행 중이면 중복 실행 방지
        if (ComponentHelper.IsMacroRunning) return;
        ComponentHelper.IsMacroRunning = true;
        ShowMacroOverlay(instance);
        // CoroutineRunner를 통해 코루틴 시작 — Postfix는 MonoBehaviour가 아니라 직접 호출 불가
        CoroutineHelper.GetOrCreateRunner().StartCoroutine(TalkMacroLoop());
    }

    private static void ShowMacroOverlay(QuickMenuUI instance)
    {
        // 기존 오버레이가 있으면 재사용, 없으면 ComponentHelper로 새로 생성
        (instance.transform.Find("MacroOverlay")?.gameObject
            ?? ComponentHelper.CreateOverlay(instance)).SetActive(true);
    }

    /// <summary>
    /// 매크로 루프 코루틴.
    /// IsMacroRunning이 false가 될 때까지 대화 → 스킵을 반복한다.
    /// Q키 또는 오버레이 종료 버튼이 IsMacroRunning을 false로 바꿔 루프를 종료시킨다.
    /// </summary>
    private static IEnumerator TalkMacroLoop()
    {
        while (ComponentHelper.IsMacroRunning)
        {
            // 대화 버튼이 활성화될 때까지 대기 (씬 전환·팝업 등으로 잠시 사라질 수 있음)
            while (!UIHelper.IsExistAndActive(
                "Singleton (DontDestroy)/AppManager/UI/QuickMenuUI(Clone)/Parent/MenuButtonLayout/Layout - Menu/BottomMenus - Link/object0"))
                yield return new WaitForSeconds(SkipButtonPollInterval);

            StepClickTalk();
            yield return new WaitForSeconds(SkipButtonPollInterval);

            // BalloonScriptUI가 열리고 Skip 버튼이 나타날 때까지 폴링
            while (!UIHelper.IsExistAndActive(
                "Singleton (DontDestroy)/AppManager/UI/BalloonScriptUI(Clone)/StorySkipUI/Mask/Layout - Button/Layout - HiddenButton/Button - Skip"))
                yield return new WaitForSeconds(SkipButtonPollInterval);

            StepShowBalloonOverlay();
            StepClickSkip();
            yield return new WaitForSeconds(LoopInterval);
        }
        ComponentHelper.IsMacroRunning = false;
    }

    // --- 매크로 단계별 클릭 메서드 ---
    // 각각 UIHelper.TryInvokeButton을 래핑해 로그 라벨을 명확히 한다.

    private static void StepClickTalk()
    {
        UIHelper.TryInvokeButton(
            "Singleton (DontDestroy)/AppManager/UI/QuickMenuUI(Clone)/Parent/MenuButtonLayout/Layout - Menu/BottomMenus - Link/object0",
            "talkButton");
    }

    private static void StepShowBalloonOverlay()
    {
        // BalloonScriptUI 위에도 오버레이를 띄워 매크로 진행 중임을 표시
        GameObject balloon = GameObject.Find("Singleton (DontDestroy)/AppManager/UI/BalloonScriptUI(Clone)");
        if (balloon == null) return;
        (balloon.transform.Find("MacroOverlay")?.gameObject
            ?? ComponentHelper.CreateOverlay(balloon.GetComponent<MonoBehaviour>())).SetActive(true);
    }

    private static void StepClickSkip()
    {
        UIHelper.TryInvokeButton(
            "Singleton (DontDestroy)/AppManager/UI/BalloonScriptUI(Clone)/StorySkipUI/Mask/Layout - Button/Layout - HiddenButton/Button - Skip",
            "skipButton");
    }
}
