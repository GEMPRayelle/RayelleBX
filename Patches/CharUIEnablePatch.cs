// CharUIEnablePatch.cs — 코스튬 탭에서 코스튬 ID·이름을 CSV에 기록하는 패치.
//
// [패치 대상 3개]
// ① CharUI.ShowUI() (파라미터 없는 오버로드)
// ② CharCostumeUI.SelectCostumeItem(CostumeListLoopScrollItem, int)
// ③ CharCostumeUI.ResetSelectCostume
//
// [이름 취득 흐름 — 2단계]
// Step1 (안전): costumeId로 int→string 전수 호출 → 로컬라이즈 타입(ὬὪὥ...) 식별
// Step2 (안전): CostumeTable(costumeId).CostumeNameTextId → 해당 타입의 메서드만 호출 → 이름
// → 팝업 원인인 "전체 타입 × textId" 조합을 피한다.
//
// [캐릭터명 접두사]
// CostumeTable.UseUniqueCharId → CharTable(charId) → 이름 textId → 로컬라이즈 → 캐릭터명
// FindCharTableMethod: int→T 정적 메서드 중 result.Id==charId인 것 찾고,
//                      int 프로퍼티를 _localizeMethod로 시험해 짧은 한국어 이름 확정
//
// [난독화 필드 (v2026-04-23)]
// costumeData : ὥὪὩὢὣὯὩὨὮὫὢ  (CostumeDBInfo)
// costumeList : ὣὩὮὠὣὩὩὣὬὡὬ  (List<T>)

using HarmonyLib;
using RayelleBX.Config;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace RayelleBX.Patches;

public class CharUIEnablePatch
{
    public static void ApplyPatches(Harmony harmony)
    {
        try
        {
            MethodInfo showUI = AccessTools.Method(typeof(CharUI), "ShowUI", new System.Type[] { });
            if (showUI == null) Plugin.Log.LogWarning("[CharUI] ShowUI() 없음");
            else
            {
                harmony.Patch(showUI, postfix: new HarmonyMethod(typeof(CharUIEnablePatch), nameof(PostfixScan)));
                Plugin.Log.LogInfo("[CharUI] ShowUI() 패치 등록");
            }

            Type scrollItemType = AccessTools.TypeByName("CostumeListLoopScrollItem");
            MethodInfo selectItem = scrollItemType != null
                ? AccessTools.Method(typeof(CharCostumeUI), "SelectCostumeItem",
                    new Type[] { scrollItemType, typeof(int) })
                : null;
            if (selectItem == null) Plugin.Log.LogWarning("[CharUI] SelectCostumeItem() 없음");
            else
            {
                harmony.Patch(selectItem, postfix: new HarmonyMethod(typeof(CharUIEnablePatch), nameof(PostfixSelectItem)));
                Plugin.Log.LogInfo("[CharUI] SelectCostumeItem() 패치 등록");
            }

            MethodInfo resetSelect = typeof(CharCostumeUI)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "ResetSelectCostume");
            if (resetSelect == null) Plugin.Log.LogWarning("[CharUI] ResetSelectCostume() 없음");
            else
            {
                harmony.Patch(resetSelect, postfix: new HarmonyMethod(typeof(CharUIEnablePatch), nameof(PostfixResetSelect)));
                Plugin.Log.LogInfo("[CharUI] ResetSelectCostume() 패치 등록");
            }
        }
        catch (Exception e) { Plugin.Log.LogError($"[CharUI] 패치 실패: {e.Message}"); }
    }

    private static void PostfixScan(CharUI __instance)
    {
        try
        {
            Transform tab = __instance.transform.Find("UIRoot/Mask/Tab - 2 - Costume");
            CharCostumeUI ui = tab?.GetComponent<CharCostumeUI>();
            if (ui == null) return;
            ScanFullCostumeList(ui);
        }
        catch (Exception ex) { Plugin.Log.LogInfo($"[CharUI] PostfixScan 예외:\n{ex}"); }
    }

    private static void PostfixSelectItem(CharCostumeUI __instance)
    {
        try
        {
            object costumeData = typeof(CharCostumeUI)
                .GetField("ὥὪὩὢὣὯὩὨὮὫὢ", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(__instance);
            if (costumeData == null) return;

            object costumeId = FindCostumeId(costumeData);
            if (costumeId == null) return;

            string name = TryGetCostumeName(costumeData);
            Plugin.Log.LogInfo($"[Costume Patch] SelectItem: ID={costumeId}, Name={name ?? "(none)"}");
            CostumeConfig.AppendMapping(costumeId, name ?? "");
        }
        catch (Exception ex) { Plugin.Log.LogInfo($"[CharUI] PostfixSelectItem 예외: {ex}"); }
    }

    private static void PostfixResetSelect(CharCostumeUI __instance, object __1)
    {
        try
        {
            int id = 0;
            if (__1 != null)
            {
                object v = __1.GetType().GetProperty("Id",
                    BindingFlags.Instance | BindingFlags.Public)?.GetValue(__1);
                if (v is int i) id = i;
            }
            if (id <= 0) return;
            Plugin.Log.LogInfo($"[Costume Patch] ResetSelect: CostumeID={id}");
            CostumeConfig.AppendMapping(id, "");
        }
        catch (Exception ex) { Plugin.Log.LogInfo($"[CharUI] PostfixResetSelect 예외:\n{ex}"); }
    }

    // ── 이름 조회 ────────────────────────────────────────────────────────────

    private static bool         _scanned              = false;
    private static Type         _localizeType         = null;
    private static MethodInfo   _costumeTableMethod   = null;
    private static PropertyInfo _costumeNameTextIdProp = null;
    private static MethodInfo   _localizeMethod       = null;

    // 캐릭터명 조회용
    private static bool         _charScanned       = false;
    private static PropertyInfo _useUniqueCharIdProp = null; // CostumeTable.UseUniqueCharId
    private static MethodInfo   _charTableMethod   = null;   // int→CharTable
    private static PropertyInfo _charNameTextIdProp = null;  // CharTable.이름 textId 프로퍼티

    // costumeData.Id(CostumeId) → CostumeTable → {캐릭터명}_{코스튬명}
    private static string TryGetCostumeName(object costumeData)
    {
        const BindingFlags instBf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        PropertyInfo idProp = costumeData.GetType()
            .GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
        if (idProp == null) return null;
        object idVal = idProp.GetValue(costumeData);
        if (!(idVal is int costumeId) || costumeId <= 0) return null;

        if (!_scanned) { _scanned = true; FindNameMethod(costumeId); }

        if (_costumeTableMethod == null || _costumeNameTextIdProp == null || _localizeMethod == null)
            return null;

        try
        {
            object tbl = _costumeTableMethod.Invoke(null, new object[] { costumeId });
            if (tbl == null) return null;

            // 코스튬명
            object tv = _costumeNameTextIdProp.GetValue(tbl);
            if (!(tv is int textId) || textId <= 0) return null;
            string costumeName = _localizeMethod.Invoke(null, new object[] { textId }) as string;
            if (string.IsNullOrEmpty(costumeName)) return null;

            // UseUniqueCharId 프로퍼티 lazy 초기화
            if (_useUniqueCharIdProp == null)
                _useUniqueCharIdProp = tbl.GetType().GetProperty("UseUniqueCharId", instBf);

            string charName = TryGetCharName(tbl);
            return string.IsNullOrEmpty(charName) ? costumeName : $"{charName}_{costumeName}";
        }
        catch (Exception ex)
        {
            Plugin.Log.LogInfo($"[DesignDB] 이름 조회 예외: {ex.Message}");
            return null;
        }
    }

    // CostumeTable에서 UseUniqueCharId → 캐릭터명
    private static string TryGetCharName(object costumeTbl)
    {
        try
        {
            if (_useUniqueCharIdProp == null) return null;
            object charIdVal = _useUniqueCharIdProp.GetValue(costumeTbl);
            if (!(charIdVal is int charId) || charId <= 0) return null;

            if (!_charScanned) { _charScanned = true; FindCharTableMethod(charId); }

            if (_charTableMethod == null || _charNameTextIdProp == null) return null;

            object charTbl = _charTableMethod.Invoke(null, new object[] { charId });
            if (charTbl == null) return null;

            object textIdVal = _charNameTextIdProp.GetValue(charTbl);
            if (!(textIdVal is int textId) || textId <= 0) return null;

            return _localizeMethod.Invoke(null, new object[] { textId }) as string;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogInfo($"[CharUI] 캐릭터명 조회 예외: {ex.Message}");
            return null;
        }
    }

    // int→T 정적 메서드 중 result.Id==charUniqueId인 것 탐색 → int 프로퍼티를 textId로 시험
    // 코스튬 테이블 클래스(charId 범위가 아닌 costumeId 범위)는 팝업 유발 → 스킵
    private static void FindCharTableMethod(int charUniqueId)
    {
        Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        if (asm == null) return;

        Type[] allTypes;
        try { allTypes = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { allTypes = ex.Types.Where(t => t != null).ToArray(); }

        const BindingFlags staticBf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags instBf   = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // 코스튬 테이블 클래스는 costumeId 전용이므로 charId를 넣으면 팝업 유발
        Type skipType = _costumeTableMethod?.DeclaringType;

        Plugin.Log.LogInfo($"[CharUI] 캐릭터 테이블 탐색 시작 (charId={charUniqueId})");

        foreach (Type t in allTypes)
        {
            if (t == skipType) continue;
            try
            {
                foreach (MethodInfo m in t.GetMethods(staticBf))
                {
                    try
                    {
                        var parms = m.GetParameters();
                        if (parms.Length != 1 || parms[0].ParameterType != typeof(int)) continue;
                        if (m.ReturnType == typeof(void) || m.ReturnType == typeof(string)) continue;

                        object result = m.Invoke(null, new object[] { charUniqueId });
                        if (result == null) continue;

                        // result.Id == charUniqueId 검증 — 캐릭터 인덱스 테이블임을 확인
                        PropertyInfo idProp = result.GetType().GetProperty("Id", instBf);
                        if (idProp == null) continue;
                        object rid = idProp.GetValue(result);
                        if (!(rid is int ridInt) || ridInt != charUniqueId) continue;

                        // int 프로퍼티를 textId로 시험 → 짧은 한국어 이름 확정
                        // textId < 10000 은 enum/코드값으로 캐릭터명 textId가 아님
                        foreach (PropertyInfo p in result.GetType().GetProperties(instBf)
                            .Where(p => p.PropertyType == typeof(int) && p.Name != "Id"))
                        {
                            try
                            {
                                object textIdVal = p.GetValue(result);
                                if (!(textIdVal is int textId)) continue;
                                if (textId < 10000) continue;

                                string name = _localizeMethod.Invoke(null, new object[] { textId }) as string;
                                if (string.IsNullOrEmpty(name)) continue;
                                if (!name.Any(c => c >= '가' && c <= '힣')) continue;
                                if (name.Length > 10 || name.Contains('\n') || name.Contains('<')) continue;

                                _charTableMethod   = m;
                                _charNameTextIdProp = p;
                                Plugin.Log.LogInfo(
                                    $"[CharUI] 캐릭터 테이블 확정: {t.Name}.{m.Name}, " +
                                    $"prop={p.Name}, charName='{name}'");
                                return;
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        Plugin.Log.LogWarning("[CharUI] 캐릭터 테이블 탐색 실패");
    }

    private static void FindNameMethod(int costumeId)
    {
        Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        if (asm == null) return;

        Type[] allTypes;
        try { allTypes = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { allTypes = ex.Types.Where(t => t != null).ToArray(); }

        const BindingFlags staticBf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags instBf   = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        Plugin.Log.LogInfo($"[DesignDB] 탐색 시작 (costumeId={costumeId})");

        // Step1: costumeId로 int→string 탐색 → 로컬라이즈 타입 식별
        Plugin.Log.LogInfo("[DesignDB] Step1: 로컬라이즈 타입 식별");
        FindLocalizationType(allTypes, staticBf, costumeId, costumeId + 1);

        // Step2-a: CostumeTable(costumeId).CostumeNameTextId 추출
        var dbTypes = new System.Collections.Generic.HashSet<Type>();
        int textId = FindCostumeNameTextId(allTypes, staticBf, instBf, costumeId, dbTypes);
        if (textId <= 0)
        {
            Plugin.Log.LogWarning("[DesignDB] CostumeNameTextId 취득 실패");
            return;
        }

        // Step2-b: 식별된 로컬라이즈 타입 내 메서드만 호출 → 팝업 없음
        if (_localizeType != null)
        {
            Plugin.Log.LogInfo(
                $"[DesignDB] Step2: {_localizeType.Name} 내 textId={textId} 탐색");
            FindLocalizeMethodInType(staticBf, textId);
        }

        // 폴백: 로컬라이즈 타입 미발견 시 전체 탐색 (팝업 가능, 1회만)
        if (_localizeMethod == null)
        {
            Plugin.Log.LogInfo("[DesignDB] Step2 폴백: 전체 타입 탐색");
            FindLocalizeMethodFallback(allTypes, staticBf, textId, dbTypes);
        }

        if (_localizeMethod == null)
            Plugin.Log.LogWarning("[DesignDB] 이름 메서드 탐색 실패");
    }

    // costumeId/altId로 int→string 메서드 탐색 → 다른 결과인 타입을 로컬라이즈 타입으로 저장
    private static void FindLocalizationType(Type[] allTypes, BindingFlags staticBf, int id, int altId)
    {
        foreach (Type t in allTypes)
        {
            try
            {
                foreach (MethodInfo m in t.GetMethods(staticBf))
                {
                    try
                    {
                        if (m.ReturnType != typeof(string)) continue;
                        var parms = m.GetParameters();
                        if (parms.Length != 1 || parms[0].ParameterType != typeof(int)) continue;

                        string r1 = m.Invoke(null, new object[] { id }) as string;
                        if (string.IsNullOrEmpty(r1) || !r1.Any(c => c >= '가' && c <= '힣')) continue;

                        string r2 = null;
                        try { r2 = m.Invoke(null, new object[] { altId }) as string; } catch { }

                        if (!string.IsNullOrEmpty(r2) && r2 != r1)
                        {
                            Plugin.Log.LogInfo(
                                $"[DesignDB] 로컬라이즈 타입: {t.Name} " +
                                $"({id})='{r1}', ({altId})='{r2}'");
                            _localizeType = t;
                            return;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
        Plugin.Log.LogWarning("[DesignDB] 로컬라이즈 타입 미발견");
    }

    // CostumeTable 반환 메서드로 CostumeNameTextId를 추출하고 dbTypes를 수집한다.
    private static int FindCostumeNameTextId(
        Type[] allTypes, BindingFlags staticBf, BindingFlags instBf, int costumeId,
        System.Collections.Generic.HashSet<Type> dbTypes)
    {
        int foundTextId = 0;
        foreach (Type t in allTypes)
        {
            try
            {
                foreach (MethodInfo m in t.GetMethods(staticBf))
                {
                    try
                    {
                        var parms = m.GetParameters();
                        if (parms.Length != 1 || parms[0].ParameterType != typeof(int)) continue;
                        if (m.ReturnType == typeof(void) || m.ReturnType == typeof(string)) continue;

                        string retName = m.ReturnType.Name;
                        if (retName.Contains("Table") || retName.Contains("Info")) dbTypes.Add(t);

                        if (foundTextId > 0 || retName != "CostumeTable") continue;

                        object result = m.Invoke(null, new object[] { costumeId });
                        if (result == null) continue;
                        PropertyInfo textIdProp = result.GetType()
                            .GetProperty("CostumeNameTextId", instBf);
                        if (textIdProp == null) continue;
                        object tv = textIdProp.GetValue(result);
                        if (!(tv is int tid) || tid <= 0) continue;

                        _costumeTableMethod   = m;
                        _costumeNameTextIdProp = textIdProp;
                        foundTextId = tid;
                        Plugin.Log.LogInfo(
                            $"[DesignDB] CostumeTable: {t.Name}.{m.Name}, CostumeNameTextId={tid}");
                    }
                    catch { }
                }
            }
            catch { }
        }
        return foundTextId;
    }

    // 로컬라이즈 타입(_localizeType) 내 메서드만 호출 → 팝업 없음
    private static void FindLocalizeMethodInType(BindingFlags staticBf, int textId)
    {
        if (_localizeType == null) return;
        int altTextId = textId - 2;

        foreach (MethodInfo m in _localizeType.GetMethods(staticBf))
        {
            try
            {
                if (m.ReturnType != typeof(string)) continue;
                var parms = m.GetParameters();
                if (parms.Length != 1 || parms[0].ParameterType != typeof(int)) continue;

                string r = m.Invoke(null, new object[] { textId }) as string;
                if (string.IsNullOrEmpty(r) || !r.Any(c => c >= '가' && c <= '힣')) continue;

                string r2 = null;
                try { r2 = m.Invoke(null, new object[] { altTextId }) as string; } catch { }

                Plugin.Log.LogInfo(
                    $"[DesignDB] 이름 메서드 후보: {_localizeType.Name}.{m.Name}" +
                    $"({textId})='{r}', ({altTextId})='{r2 ?? "(null)"}'");

                if (_localizeMethod == null)
                {
                    _localizeMethod = m;
                    Plugin.Log.LogInfo($"[DesignDB] 이름 메서드 확정: {_localizeType.Name}.{m.Name}");
                    return;
                }
            }
            catch { }
        }
    }

    // 로컬라이즈 타입 미발견 시 전체 탐색 폴백 (팝업 1회 가능)
    private static void FindLocalizeMethodFallback(
        Type[] allTypes, BindingFlags staticBf, int textId,
        System.Collections.Generic.HashSet<Type> dbTypes)
    {
        int altTextId = textId - 2;
        foreach (Type t in allTypes)
        {
            if (dbTypes.Contains(t)) continue;
            try
            {
                foreach (MethodInfo m in t.GetMethods(staticBf))
                {
                    try
                    {
                        if (m.ReturnType != typeof(string)) continue;
                        var parms = m.GetParameters();
                        if (parms.Length != 1 || parms[0].ParameterType != typeof(int)) continue;

                        string r = m.Invoke(null, new object[] { textId }) as string;
                        if (string.IsNullOrEmpty(r) || !r.Any(c => c >= '가' && c <= '힣')) continue;

                        string r2 = null;
                        try { r2 = m.Invoke(null, new object[] { altTextId }) as string; } catch { }

                        Plugin.Log.LogInfo(
                            $"[DesignDB] 폴백 후보: {t.Name}.{m.Name}" +
                            $"({textId})='{r}', ({altTextId})='{r2 ?? "(null)"}'");

                        if (_localizeMethod == null)
                        {
                            _localizeMethod = m;
                            Plugin.Log.LogInfo($"[DesignDB] 폴백 확정: {t.Name}.{m.Name}");
                            return;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    // ── 유틸리티 ─────────────────────────────────────────────────────────────

    private static void ScanFullCostumeList(CharCostumeUI costumeUI)
    {
        try
        {
            FieldInfo listField = typeof(CharCostumeUI)
                .GetField("ὣὩὮὠὣὩὩὣὬὡὬ", BindingFlags.Instance | BindingFlags.NonPublic);
            if (listField == null) return;

            object list = listField.GetValue(costumeUI);
            if (!(list is IEnumerable items)) return;

            int added = 0;
            foreach (object item in items)
            {
                object id = FindCostumeId(item);
                if (id == null) continue;
                if (CostumeConfig.AppendMapping(id, "")) added++;
            }
            if (added > 0) Plugin.Log.LogInfo($"[Costume Patch] 전체 목록 신규 {added}개 기록");
        }
        catch (Exception ex) { Plugin.Log.LogInfo($"[Costume Patch] ScanFullCostumeList 예외: {ex}"); }
    }

    private static object FindCostumeId(object costumeData)
    {
        const BindingFlags bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        Type t = costumeData.GetType();

        string[] candidates = {
            "ὪὫὫὢὩὦὤὪὧὫὡ", "ὢὢὮὬὫὨὪὮὥὩὥ",
            "CostumeId", "CostumeID", "costumeId", "ID", "Id", "id"
        };

        foreach (string name in candidates)
        {
            PropertyInfo pi = t.GetProperty(name, bf);
            if (pi != null) return pi.GetValue(costumeData);
        }
        foreach (string name in candidates)
        {
            FieldInfo fi = t.GetField(name, bf);
            if (fi != null) return fi.GetValue(costumeData);
        }

        PropertyInfo propFallback = t.GetProperties(bf).FirstOrDefault(p => p.PropertyType == typeof(int));
        if (propFallback != null)
        {
            Plugin.Log.LogWarning($"[CharUIEnablePatch] 폴백(프로퍼티): {propFallback.Name}");
            return propFallback.GetValue(costumeData);
        }

        FieldInfo fieldFallback = t.GetFields(bf)
            .Where(f => !f.Name.Contains("BackingField"))
            .FirstOrDefault(f => f.FieldType == typeof(int));
        if (fieldFallback != null)
        {
            object val = fieldFallback.GetValue(costumeData);
            Plugin.Log.LogWarning($"[CharUIEnablePatch] 폴백(필드): {fieldFallback.Name} = {val}");
            return val;
        }

        Plugin.Log.LogWarning($"[CharUIEnablePatch] CostumeID 없음 — {t.Name}");
        return null;
    }
}
