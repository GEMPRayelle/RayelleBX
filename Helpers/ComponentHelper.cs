using System.Reflection;
using TMPro;
using UnityEngine;
using RayelleBX;

namespace RayelleBX.Helpers;

public static class ComponentHelper
{
    private static void CreateFontLocalizer(GameObject go, Component textTarget)
    {
        FontLocalizer fontLocalizer = go.AddComponent<FontLocalizer>();

        FieldInfo fTextTarget = typeof(FontLocalizer).GetField("_textTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fTextTarget != null) fTextTarget.SetValue(fontLocalizer, textTarget);
        else Plugin.Log.LogWarning("[ComponentHelper] FontLocalizer._textTarget 필드를 찾을 수 없음");

        FieldInfo fFontName = typeof(FontLocalizer).GetField("ὡὥὢὬὡὭὯὭὥὦὢ", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fFontName != null) fFontName.SetValue(fontLocalizer, "SCDreamExtraBold");
        else Plugin.Log.LogWarning("[ComponentHelper] FontLocalizer fontName 필드를 찾을 수 없음");

        FieldInfo fFontMaterial = typeof(FontLocalizer).GetField("ὮὯὡὨὬὯὭὬὯὫὫ", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fFontMaterial != null) fFontMaterial.SetValue(fontLocalizer, "SCDreamExtraBold Material");
        else Plugin.Log.LogWarning("[ComponentHelper] FontLocalizer fontMaterial 필드를 찾을 수 없음");

        MethodInfo mApply = typeof(FontLocalizer).GetMethod("ὤὮὫὯὦὭὥὦὫὩὢ", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mApply != null) mApply.Invoke(fontLocalizer, null);
        else Plugin.Log.LogWarning("[ComponentHelper] FontLocalizer apply 메서드를 찾을 수 없음");
    }

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
