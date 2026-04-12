// GachaMacroUIEnablePatch.cs — 가챠 결과 화면에 자동뽑기 버튼을 추가하는 패치.
//
// [원본 참조: BrownUtil.Patches.GachaResultUIEnablePatch / GachaResultUIPatch]
//
// [패치 구조]
// ① GachaResultUI.SetActive(bool)  → 결과 UI 활성화 시 Button - Redraw 를 복제해 자동뽑기 버튼 생성
// ② GachaResultUI.SetResult(...)   → 결과 데이터 수신 시 UR·위시 코스튬 수 판정 후 LastConditionMet 설정
//
// [UI 경로]
// Layout - InfiniteGachaButton 하위에 Button - Redraw (다시뽑기) 와 Button - AutoGacha (자동뽑기) 병렬 배치
// 다시뽑기 클릭 → GachaInfinitePopupUI YES 클릭 → Skip 반복 → SetResult 대기 → 조건 판정 → 반복
//
// [UR 판정]
// SetResult __0(List) 첫 번째 파라미터의 각 항목에서 BackingField(int)를 읽어 CostumeID를 추출.
// CostumeConfig.LoadAllCostumes()에 포함된 ID = 5성, GachaWishCostumes에 포함 = 위시.

using BepInEx;
using HarmonyLib;
using RayelleBX.Config;
using RayelleBX.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RayelleBX.Patches;

public class GachaMacroUIEnablePatch
{
    // ── UI 경로 상수 ──────────────────────────────────────────────────────────
    private const string RedrawBtnRelPath   = "UIRoot/Mask/Layout - InfiniteGachaButton/Button - Redraw";
    private const string AutoBtnRelPath     = "UIRoot/Mask/Layout - InfiniteGachaButton/Button - AutoGacha";
    private const string RedrawBtnFullPath  = "Singleton (DontDestroy)/AppManager/UI/GachaResultUI(Clone)/UIRoot/Mask/Layout - InfiniteGachaButton/Button - Redraw";
    private const string YesBtnFullPath     = "Singleton (DontDestroy)/AppManager/UI/GachaInfinitePopupUI(Clone)/Button - background/Parent/Image - Backgrond/ButtonYesNo/Button - YES";
    private const string SkipBtnFullPath    = "Singleton (DontDestroy)/AppManager/UI/GachaResultUI(Clone)/UIRoot/Mask/Button - Skip";
    private const float  PollInterval       = 0.2f;
    private const float  SkipPollTimeout    = 5f;

    // ── 결과 판정 상태 ────────────────────────────────────────────────────────
    public static bool LastConditionMet { get; private set; }
    public static bool ResultReceived   { get; set; }

    // ── 수동 패치 등록 ────────────────────────────────────────────────────────

    public static void ApplyPatches(Harmony harmony)
    {
        TryPatch(harmony, typeof(GachaResultUI), "SetActive",
            nameof(SetActive_Postfix),  "GachaResultUI.SetActive");

        TryPatch(harmony, typeof(GachaResultUI), "SetResult",
            nameof(SetResult_Postfix),  "GachaResultUI.SetResult");
    }

    private static void TryPatch(Harmony harmony,
        Type targetType, string methodName, string postfixName, string label)
    {
        try
        {
            MethodInfo target = AccessTools.Method(targetType, methodName);
            if (target == null)
            {
                Plugin.Log.LogWarning($"[GachaMacro] 메서드 없음: {label}");
                return;
            }
            harmony.Patch(target,
                postfix: new HarmonyMethod(typeof(GachaMacroUIEnablePatch), postfixName));
            Plugin.Log.LogInfo($"[GachaMacro] 패치 등록: {label}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[GachaMacro] 패치 실패: {label} — {e.Message}");
        }
    }

    // ── Postfix ① GachaResultUI.SetActive(bool) ──────────────────────────────
    // __0 = bool active

    private static void SetActive_Postfix(GachaResultUI __instance, bool __0)
    {
        try
        {
            if (!__0) return; // 비활성화 시 무시

            // Button - Redraw 탐색 (상대 경로)
            Transform redrawTransform = __instance.transform.Find(RedrawBtnRelPath);
            if (redrawTransform == null)
            {
                Plugin.Log.LogWarning("[GachaMacro] Button - Redraw 없음");
                return;
            }

            // 이미 자동뽑기 버튼이 있으면 스킵
            if (__instance.transform.Find(AutoBtnRelPath) != null)
                return;

            // Button - Redraw 를 복제해 자동뽑기 버튼 생성
            GameObject autoBtn = UnityEngine.Object.Instantiate(
                redrawTransform.gameObject, redrawTransform.parent);
            autoBtn.name = "Button - AutoGacha";

            // TutorialFocusTarget 제거 (있으면 튜토리얼 시스템과 충돌 가능)
            TutorialFocusTarget tft = autoBtn.GetComponent<TutorialFocusTarget>();
            if (tft != null) UnityEngine.Object.Destroy(tft);

            // 텍스트 변경
            Transform textTransform = autoBtn.transform.Find("Text - Title");
            if (textTransform != null)
            {
                TextMeshProUGUI tmp = textTransform.GetComponent<TextMeshProUGUI>();
                if (tmp != null) tmp.text = "자동 뽑기";
            }

            // 클릭 리스너 교체
            Button btn = autoBtn.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener((UnityAction)StartAutoGacha);
            }

            Plugin.Log.LogInfo("[GachaMacro] 자동 뽑기 버튼 생성 완료");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[GachaMacro] 버튼 생성 실패: {ex.Message}");
        }
    }

    // ── Postfix ② GachaResultUI.SetResult(...) ────────────────────────────────
    // __0 = List<(obfuscated)> 가챠 결과 항목 목록

    private static void SetResult_Postfix(GachaResultUI __instance, object __0)
    {
        try
        {
            int minUr       = PluginConfig.GachaMinUR;
            int essentialUr = PluginConfig.GachaEssentialUR;
            HashSet<int> wishSet    = PluginConfig.GachaWishCostumes;
            HashSet<int> costumeSet = CostumeConfig.LoadAllCostumes();

            int urCount   = 0;
            int wishCount = 0;

            // __0 을 IEnumerable로 순회 (List<ObfuscatedType> → IEnumerable)
            IEnumerable list = __0 as IEnumerable;
            if (list != null)
            {
                foreach (object item in list)
                {
                    // BackingField를 찾아 CostumeID 추출 (int 형 auto-property backing field)
                    FieldInfo backingField = item.GetType()
                        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .FirstOrDefault(f => f.Name.Contains("BackingField"));

                    if (backingField == null) continue;

                    object raw = backingField.GetValue(item);
                    int costumeId;
                    if (raw is int i) costumeId = i;
                    else if (!int.TryParse(raw?.ToString(), out costumeId)) continue;

                    bool isUR   = costumeSet.Count > 0 && costumeSet.Contains(costumeId);
                    bool isWish = wishSet.Count  > 0 && wishSet.Contains(costumeId);

                    if (isUR)   urCount++;
                    if (isWish) wishCount++;

                    Plugin.Log.LogInfo(
                        $"[GachaMacro] CostumeID={costumeId} UR={isUR} Wish={isWish}");
                }
            }

            Plugin.Log.LogInfo(
                $"[GachaMacro] 결과 UR={urCount} Wish={wishCount} / MinUR={minUr} EssentialUR={essentialUr}");

            LastConditionMet = urCount >= minUr && wishCount >= essentialUr;
            ResultReceived   = true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[GachaMacro] SetResult 처리 실패: {ex.Message}");
            ResultReceived = true; // 실패해도 루프를 멈추지 않도록
        }
    }

    // ── 자동뽑기 시작 ─────────────────────────────────────────────────────────

    private static void StartAutoGacha()
    {
        if (ComponentHelper.IsMacroRunning)
        {
            Plugin.Log.LogInfo("[GachaMacro] 이미 매크로 실행 중");
            return;
        }
        ComponentHelper.IsMacroRunning = true;
        ResultReceived = false;
        CoroutineHelper.GetOrCreateRunner().StartCoroutine(AutoGachaLoop());
    }

    // ── 자동뽑기 루프 ─────────────────────────────────────────────────────────

    private static IEnumerator AutoGachaLoop()
    {
        float stepDelay  = PluginConfig.GachaStepDelay;
        float resultWait = PluginConfig.GachaWaitResultTimeout;

        while (ComponentHelper.IsMacroRunning)
        {
            // ① Button - Redraw 활성화 대기
            yield return WaitUntilActive(RedrawBtnFullPath);
            if (!ComponentHelper.IsMacroRunning) break;

            // ② Button - Redraw 클릭 (다시뽑기)
            UIHelper.TryInvokeButton(RedrawBtnFullPath, "Button - Redraw");
            yield return new WaitForSeconds(stepDelay);

            // ③ GachaInfinitePopupUI YES 버튼 대기 및 클릭
            yield return WaitUntilActive(YesBtnFullPath);
            if (!ComponentHelper.IsMacroRunning) break;

            UIHelper.TryInvokeButton(YesBtnFullPath, "Button - YES");
            yield return new WaitForSeconds(stepDelay);

            // ④ Skip 버튼 반복 클릭 (결과 애니메이션 스킵)
            yield return PressSkipUntilGone();
            if (!ComponentHelper.IsMacroRunning) break;

            // ⑤ SetResult 대기
            ResultReceived = false;
            yield return WaitForResult(resultWait);

            // ⑥ 조건 판정
            if (LastConditionMet)
            {
                Plugin.Log.LogInfo("[GachaMacro] 조건 충족! 자동뽑기 종료");
                ComponentHelper.IsMacroRunning = false;
                break;
            }

            yield return new WaitForSeconds(stepDelay);
        }

        ComponentHelper.IsMacroRunning = false;
        Plugin.Log.LogInfo("[GachaMacro] 루프 종료");
    }

    // ── 헬퍼 코루틴 ──────────────────────────────────────────────────────────

    private static IEnumerator WaitUntilActive(string path)
    {
        while (ComponentHelper.IsMacroRunning)
        {
            GameObject go = GameObject.Find(path);
            if (go != null && go.activeInHierarchy) yield break;
            yield return new WaitForSeconds(PollInterval);
        }
    }

    private static IEnumerator WaitUntilActiveWithTimeout(string path, float timeout)
    {
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            GameObject go = GameObject.Find(path);
            if (go != null && go.activeInHierarchy) yield break;
            yield return new WaitForSeconds(PollInterval);
            elapsed += PollInterval;
        }
    }

    private static IEnumerator PressSkipUntilGone()
    {
        float elapsed = 0f;
        yield return WaitUntilActiveWithTimeout(SkipBtnFullPath, SkipPollTimeout);

        while (ComponentHelper.IsMacroRunning && elapsed < SkipPollTimeout)
        {
            GameObject skipGo = GameObject.Find(SkipBtnFullPath);
            if (skipGo == null || !skipGo.activeInHierarchy) yield break;

            UIHelper.TryInvokeButton(skipGo, "Button - Skip");
            yield return new WaitForSeconds(PollInterval);
            elapsed += PollInterval;
        }
    }

    private static IEnumerator WaitForResult(float timeout)
    {
        for (float elapsed = 0f; !ResultReceived && elapsed < timeout; elapsed += 0.1f)
            yield return new WaitForSeconds(0.1f);
    }
}
