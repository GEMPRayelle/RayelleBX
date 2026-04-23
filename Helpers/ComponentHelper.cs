// ComponentHelper.cs — UI 컴포넌트 생성 헬퍼.
//
// [배경: FontLocalizer란?]
// BrownDust 2는 언어 설정에 따라 폰트를 동적으로 교체하는 FontLocalizer 컴포넌트를 사용한다.
// 플러그인에서 직접 TextMeshProUGUI를 생성하면 게임의 폰트 시스템을 거치지 않아
// 폰트가 깨지거나 기본 폰트로 표시된다.
// 따라서 TextMeshProUGUI 생성 후 FontLocalizer도 함께 붙여줘야 한다.
//
// [주의: 난독화 필드/메서드]
// FontLocalizer의 private 필드명과 메서드명이 난독화되어 있다.
// 리플렉션으로 접근하며, 게임 업데이트 시 이름이 바뀌면 Warning만 출력하고 폴백(기본 폰트)한다.
// 현재 기준 필드명은 CLAUDE.md의 "FontLocalizer 필드" 항목을 참조.

using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using RayelleBX;

namespace RayelleBX.Helpers;

public static class ComponentHelper
{
    /// <summary>매크로가 현재 실행 중인지 여부. CoroutineRunner가 Q키로 중단시킨다.</summary>
    public static bool IsMacroRunning;

    /// <summary>
    /// 주어진 GameObject에 FontLocalizer 컴포넌트를 추가하고
    /// 리플렉션으로 private 필드를 설정한 뒤 apply 메서드를 호출한다.
    /// 필드/메서드를 찾지 못하면 크래시 없이 Warning만 출력한다.
    /// </summary>
    /// <param name="go">FontLocalizer를 추가할 GameObject</param>
    /// <param name="textTarget">폰트를 적용할 TextMeshProUGUI 컴포넌트</param>
    private static void CreateFontLocalizer(GameObject go, Component textTarget)
    {
        FontLocalizer fontLocalizer = go.AddComponent<FontLocalizer>();

        // _textTarget : FontLocalizer가 폰트를 적용할 TMP 컴포넌트 참조
        FieldInfo fTextTarget = typeof(FontLocalizer).GetField("_textTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fTextTarget != null) fTextTarget.SetValue(fontLocalizer, textTarget);
        else Plugin.Log.LogWarning("[ComponentHelper] FontLocalizer._textTarget 필드를 찾을 수 없음");

        // 난독화된 fontName 필드 — 사용할 폰트 에셋 이름을 지정 (v2026-04-23)
        FieldInfo fFontName = typeof(FontLocalizer).GetField("ὩὠὮὮὢὮὭὤὡὥὧ", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fFontName != null) fFontName.SetValue(fontLocalizer, "SCDreamExtraBold");
        else Plugin.Log.LogWarning("[ComponentHelper] FontLocalizer fontName 필드를 찾을 수 없음");

        // 난독화된 fontMaterial 필드 — 폰트에 적용할 머티리얼 이름을 지정 (v2026-04-23)
        FieldInfo fFontMaterial = typeof(FontLocalizer).GetField("ὦὫὭὥὢὠὮὭὩὦὯ", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fFontMaterial != null) fFontMaterial.SetValue(fontLocalizer, "SCDreamExtraBold Material");
        else Plugin.Log.LogWarning("[ComponentHelper] FontLocalizer fontMaterial 필드를 찾을 수 없음");

        // apply 메서드 — void, 파라미터 없음, FontLocalizer 선언, 난독화 이름으로 자동 탐색
        MethodInfo mApply = FindFontLocalizerApplyMethod();
        if (mApply != null) mApply.Invoke(fontLocalizer, null);
        else Plugin.Log.LogWarning("[ComponentHelper] FontLocalizer apply 메서드를 찾을 수 없음");
    }

    // FontLocalizer apply 메서드 탐색.
    // apply 메서드: _textTarget + fontName + fontMaterial → 외부 유틸 호출 → ret
    // 하드코딩 이름이 없으면 void/파라미터 없음/선언 타입/비ASCII 조건으로 자동 탐색한다.
    private static MethodInfo FindFontLocalizerApplyMethod()
    {
        const BindingFlags bf = BindingFlags.Instance | BindingFlags.NonPublic;

        // 현재 알려진 apply 메서드 이름 (v2026-04-23)
        MethodInfo m = typeof(FontLocalizer).GetMethod("ὢὫὨὪὮὤὧὭὤὥὡ", bf);
        if (m != null) return m;

        // 폴백: void, 파라미터 없음, FontLocalizer 직접 선언, 난독화 이름
        var candidates = typeof(FontLocalizer).GetMethods(bf)
            .Where(x =>
                x.DeclaringType == typeof(FontLocalizer) &&
                x.ReturnType == typeof(void) &&
                x.GetParameters().Length == 0 &&
                !x.IsSpecialName &&
                x.Name.Any(c => c > 127))
            .ToList();

        if (candidates.Count == 1)
        {
            Plugin.Log.LogInfo($"[ComponentHelper] FontLocalizer apply 자동 발견: {candidates[0].Name}");
            return candidates[0];
        }
        if (candidates.Count > 1)
        {
            Plugin.Log.LogWarning(
                $"[ComponentHelper] FontLocalizer apply 후보 {candidates.Count}개, 첫 번째 사용: " +
                string.Join(", ", candidates.Select(x => x.Name)));
            return candidates[0];
        }
        return null;
    }

    /// <summary>
    /// TextMeshProUGUI를 생성하고 게임의 FontLocalizer 시스템을 연결한다.
    /// 유니티 에디터에서 TextMeshPro 컴포넌트를 추가한 뒤 폰트를 지정하는 것과 동일한 역할을 코드로 수행한다.
    /// </summary>
    /// <param name="go">TMP를 추가할 GameObject</param>
    /// <param name="text">초기 텍스트 내용</param>
    /// <param name="textColor">텍스트 색상</param>
    /// <param name="fontSize">폰트 크기 (기본값 20)</param>
    public static void CreateTMPro(GameObject go, string text, Color textColor, int fontSize = 20)
    {
        TextMeshProUGUI textTarget = go.AddComponent<TextMeshProUGUI>();
        textTarget.text = text;
        textTarget.alignment = TextAlignmentOptions.Center;
        textTarget.color = textColor;
        textTarget.fontSize = fontSize;
        textTarget.fontSizeMax = 72f;
        textTarget.fontSizeMin = 18f;
        textTarget.enableWordWrapping = false;
        CreateFontLocalizer(go, textTarget);
    }

    /// <summary>
    /// RectTransform을 추가하고 anchorMin/Max를 0/1로 설정해 부모 전체를 채운다.
    /// 오버레이처럼 부모 크기에 꽉 맞춰야 하는 UI에 사용한다.
    /// </summary>
    public static void CreateRectTransForm(GameObject go)
    {
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 매크로 진행 중임을 표시하는 반투명 오버레이를 생성한다.
    /// 구성: 검정 배경 + "자동 클릭 중..." 텍스트 + 빨간 종료 버튼.
    /// 종료 버튼 클릭 시 QuickMenu·BalloonScript의 오버레이를 비활성화하고
    /// IsMacroRunning을 false로 바꿔 모든 매크로 코루틴을 중단시킨다.
    /// </summary>
    /// <param name="parent">오버레이를 자식으로 붙일 컴포넌트 (transform.parent로 사용)</param>
    public static GameObject CreateOverlay(Component parent)
    {
        // 오버레이 루트 — 부모 전체를 덮도록 앵커를 0/1로 설정
        GameObject overlay = new GameObject("MacroOverlay");
        overlay.transform.SetParent(parent.transform, false);

        RectTransform rtOverlay = overlay.AddComponent<RectTransform>();
        rtOverlay.anchorMin = Vector2.zero;
        rtOverlay.anchorMax = Vector2.one;
        rtOverlay.offsetMin = Vector2.zero;
        rtOverlay.offsetMax = Vector2.zero;
        // 반투명 검정 배경 — 뒤쪽 UI를 흐리게 가려 매크로 중임을 시각적으로 표시
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

        // 중앙 안내 텍스트 — anchoredPosition(0,0) = 부모 중앙
        GameObject textGo = new GameObject("OverlayText");
        textGo.transform.SetParent(overlay.transform, false);
        RectTransform rtText = textGo.AddComponent<RectTransform>();
        rtText.anchorMin = new Vector2(0.5f, 0.5f);
        rtText.anchorMax = new Vector2(0.5f, 0.5f);
        rtText.anchoredPosition = Vector2.zero;
        CreateTMPro(textGo, "자동 클릭 중...", Color.white);

        // 종료 버튼 — 중앙 아래쪽(y=0.3)에 배치
        GameObject stopGo = new GameObject("StopButton");
        stopGo.transform.SetParent(overlay.transform, false);
        RectTransform rtStop = stopGo.AddComponent<RectTransform>();
        rtStop.anchorMin = new Vector2(0.5f, 0.3f);
        rtStop.anchorMax = new Vector2(0.5f, 0.3f);
        rtStop.sizeDelta = new Vector2(160f, 40f);
        rtStop.anchoredPosition = Vector2.zero;
        stopGo.AddComponent<Image>().color = new Color(1f, 0f, 0f, 0.8f);
        stopGo.AddComponent<Button>().onClick.AddListener((UnityAction)(() =>
        {
            try
            {
                Plugin.Log.LogInfo("Macro stopped");
                if (overlay != null) overlay.SetActive(false);
                GameObject.Find("Singleton (DontDestroy)/AppManager/UI/BalloonScriptUI(Clone)")
                    ?.transform.Find("MacroOverlay")?.gameObject.SetActive(false);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[ComponentHelper] Overlay 종료 예외:\n{e}");
            }
            finally
            {
                IsMacroRunning = false;
            }
        }));

        GameObject stopTextGo = new GameObject("StopButtonText");
        stopTextGo.transform.SetParent(stopGo.transform, false);
        CreateTMPro(stopTextGo, "종료", Color.white, 24);

        // 초기 상태는 비활성 — StartMacro()에서 SetActive(true)로 표시
        overlay.SetActive(false);
        return overlay;
    }
}
