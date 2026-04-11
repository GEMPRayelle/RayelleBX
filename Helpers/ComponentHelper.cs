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

using System.Reflection;
using TMPro;
using UnityEngine;
using RayelleBX;

namespace RayelleBX.Helpers;

public static class ComponentHelper
{
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

        // 난독화된 fontName 필드 — 사용할 폰트 에셋 이름을 지정
        FieldInfo fFontName = typeof(FontLocalizer).GetField("ὡὥὢὬὡὭὯὭὥὦὢ", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fFontName != null) fFontName.SetValue(fontLocalizer, "SCDreamExtraBold");
        else Plugin.Log.LogWarning("[ComponentHelper] FontLocalizer fontName 필드를 찾을 수 없음");

        // 난독화된 fontMaterial 필드 — 폰트에 적용할 머티리얼 이름을 지정
        FieldInfo fFontMaterial = typeof(FontLocalizer).GetField("ὮὯὡὨὬὯὭὬὯὫὫ", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fFontMaterial != null) fFontMaterial.SetValue(fontLocalizer, "SCDreamExtraBold Material");
        else Plugin.Log.LogWarning("[ComponentHelper] FontLocalizer fontMaterial 필드를 찾을 수 없음");

        // 난독화된 apply 메서드 — 위에서 설정한 폰트를 실제로 TextMeshProUGUI에 적용
        MethodInfo mApply = typeof(FontLocalizer).GetMethod("ὤὮὫὯὦὭὥὦὫὩὢ", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mApply != null) mApply.Invoke(fontLocalizer, null);
        else Plugin.Log.LogWarning("[ComponentHelper] FontLocalizer apply 메서드를 찾을 수 없음");
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
}
