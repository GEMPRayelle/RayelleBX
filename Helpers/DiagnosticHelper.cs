// DiagnosticHelper.cs — 게임 업데이트 후 호환성 진단을 위한 헬퍼.
// Plugin.Awake()에서 패치 등록 완료 후 한 번만 호출된다.
//
// [진단 항목]
// 1. Assembly-CSharp 버전 — 어느 게임 버전에서 터졌는지 로그로 확인
// 2. 패치 대상 타입·메서드 존재 여부 — 없으면 사용 가능한 목록을 함께 출력
// 3. 난독화 필드·메서드 존재 여부 — 없으면 현재 필드 전체 목록을 출력
//
// BepInEx 로그( BepInEx/LogOutput.log )에서 [Diagnostic] 태그로 검색하면 된다.

using System;
using System.Linq;
using System.Reflection;

namespace RayelleBX.Helpers;

public static class DiagnosticHelper
{
    public static void RunStartupDiagnostics()
    {
        Plugin.Log.LogInfo("=== [Diagnostic] 시작 ===");
        try
        {
            Assembly gameAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (gameAsm == null)
            {
                Plugin.Log.LogWarning("[Diagnostic] Assembly-CSharp를 찾을 수 없음");
                return;
            }
            Plugin.Log.LogInfo($"[Diagnostic] Assembly-CSharp 버전: {gameAsm.GetName().Version}");

            CheckPatchTargets(gameAsm);
            ScanFontLocalizer(gameAsm);
            ScanCharCostumeUI(gameAsm);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Diagnostic] 진단 중 예외: {e.Message}");
        }
        Plugin.Log.LogInfo("=== [Diagnostic] 완료 ===");
    }

    // Assembly.GetTypes()는 일부 타입 로드 실패 시 ReflectionTypeLoadException을 던질 수 있다.
    // 예외가 발생해도 이미 로드된 타입 목록에서 이름으로 탐색한다.
    private static Type FindType(Assembly asm, string typeName)
    {
        try
        {
            return asm.GetTypes().FirstOrDefault(t => t.Name == typeName);
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.FirstOrDefault(t => t != null && t.Name == typeName);
        }
    }

    private static void CheckPatchTargets(Assembly asm)
    {
        // (타입명, 메서드명, 파라미터 타입 배열) — null이면 이름만으로 첫 번째 매치
        (string typeName, string methodName, Type[] parms)[] targets = {
            ("QuickMenuUI",            "SetMenu",           null),
            ("CharRecoveryUI",         "SetUI",             null),
            ("CharUI",                 "ShowUI",            Array.Empty<Type>()),
            ("GachaResultUI",          "SetActive",         null),
            ("GachaResultUI",          "SetResult",         null),
        };

        foreach (var (typeName, methodName, parms) in targets)
        {
            try
            {
                Type t = FindType(asm, typeName);
                if (t == null)
                {
                    Plugin.Log.LogWarning($"[Diagnostic] 타입 없음: {typeName}");
                    continue;
                }

                const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                MethodInfo m = parms != null
                    ? t.GetMethod(methodName, bf, null, parms, null)
                    : t.GetMethods(bf).FirstOrDefault(x => x.Name == methodName);

                if (m == null)
                {
                    string list = string.Join(", ",
                        t.GetMethods(bf).Select(x => x.Name).Distinct()
                         .Where(n => !n.StartsWith("get_") && !n.StartsWith("set_"))
                         .OrderBy(n => n));
                    Plugin.Log.LogWarning(
                        $"[Diagnostic] 메서드 없음: {typeName}.{methodName} → [{list}]");
                }
                else
                {
                    Plugin.Log.LogInfo($"[Diagnostic] OK: {typeName}.{methodName}");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Diagnostic] 검사 예외: {typeName}.{methodName} — {e.Message}");
            }
        }
    }

    private static void ScanFontLocalizer(Assembly asm)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static |
                                  BindingFlags.Public | BindingFlags.NonPublic;
        Type t = FindType(asm, "FontLocalizer");
        if (t == null) { Plugin.Log.LogWarning("[Diagnostic] FontLocalizer 타입 없음"); return; }

        // 현재 코드에서 사용 중인 필드 이름 (v2026-04-23)
        CheckField(t, "ὩὠὮὮὢὮὭὤὡὥὧ", "fontName",     all);
        CheckField(t, "ὦὫὭὥὢὠὮὭὩὦὯ", "fontMaterial", all);

        // apply 메서드 이름 직접 확인 (v2026-04-23)
        CheckMethod(t, "ὢὫὨὪὮὤὧὭὤὥὡ", "apply", all);
    }

    private static void ScanCharCostumeUI(Assembly asm)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static |
                                  BindingFlags.Public | BindingFlags.NonPublic;
        Type t = FindType(asm, "CharCostumeUI");
        if (t == null) { Plugin.Log.LogWarning("[Diagnostic] CharCostumeUI 타입 없음"); return; }

        // 현재 코드에서 사용 중인 필드 이름 (v2026-04-23: CostumeDBInfo 타입)
        FieldInfo costumeDataField = t.GetField("ὥὪὩὢὣὯὩὨὮὫὢ", all);
        if (costumeDataField == null)
        {
            string allFields = string.Join(", ",
                t.GetFields(all)
                 .Where(f => !f.Name.Contains("BackingField"))
                 .Select(f => $"{f.Name}:{f.FieldType.Name}"));
            Plugin.Log.LogWarning(
                $"[Diagnostic] CharCostumeUI.costumeData 필드 없음 → [{allFields}]");
        }
        else
        {
            Plugin.Log.LogInfo(
                $"[Diagnostic] OK: CharCostumeUI.costumeData (field) : {costumeDataField.FieldType.Name}");

            // CostumeDBInfo의 프로퍼티 전체 목록 출력 — CostumeID 이름 특정에 사용
            Type dataType = costumeDataField.FieldType;
            string allProps = string.Join(", ",
                dataType.GetProperties(all)
                        .Select(p => $"{p.Name}:{p.PropertyType.Name}"));
            Plugin.Log.LogInfo($"[Diagnostic] {dataType.Name} 프로퍼티 목록: [{allProps}]");
        }
    }

    private static void CheckField(Type t, string obfName, string alias, BindingFlags bf)
    {
        if (t.GetField(obfName, bf) != null)
        {
            Plugin.Log.LogInfo($"[Diagnostic] OK: {t.Name}.{alias} (field)");
        }
        else
        {
            string allFields = string.Join(", ",
                t.GetFields(bf)
                 .Where(f => !f.Name.Contains("BackingField"))
                 .Select(f => $"{f.Name}:{f.FieldType.Name}"));
            Plugin.Log.LogWarning(
                $"[Diagnostic] {t.Name}.{alias} 필드 없음 → [{allFields}]");
        }
    }

    private static void CheckMethod(Type t, string obfName, string alias, BindingFlags bf)
    {
        if (t.GetMethod(obfName, bf) != null)
        {
            Plugin.Log.LogInfo($"[Diagnostic] OK: {t.Name}.{alias} (method)");
        }
        else
        {
            string allMethods = string.Join(", ",
                t.GetMethods(bf).Select(m => m.Name).Distinct()
                 .Where(n => !n.StartsWith("get_") && !n.StartsWith("set_"))
                 .OrderBy(n => n));
            Plugin.Log.LogWarning(
                $"[Diagnostic] {t.Name}.{alias} 메서드 없음 → [{allMethods}]");
        }
    }
}
