// CharUIEnablePatch.cs — 코스튬 탭 열 때마다 코스튬 ID·이름을 CSV에 기록하는 패치.
//
// [패치 대상]
// CharUI.ShowUI() (파라미터 없는 오버로드) — 코스튬 탭이 열릴 때 호출된다.
// 구버전 SetUI() 는 게임 업데이트 후 ShowUI() 로 이름이 변경됨.
//
// [동작 흐름]
// 1. "Tab - 2 - Costume" 하위의 CharCostumeUI 컴포넌트를 찾는다
// 2. 리플렉션으로 난독화 필드에서 코스튬 데이터 객체를 꺼낸다
// 3. 코스튬 데이터 객체의 난독화 프로퍼티에서 CostumeID를 읽는다
// 4. UI 텍스트에서 캐릭터명·코스튬명을 읽어 "{캐릭터명}_{코스튬명}" 형태로 조합
// 5. CostumeConfig.AppendMapping()으로 CSV에 기록
//
// [난독화 필드/프로퍼티]
// CharCostumeUI의 코스튬 데이터 필드 : ὩὠὬὣὥὮὦὢὩὧὭ (v2026-04-11 기준)
// 코스튬 데이터의 CostumeID 프로퍼티 : ὪὫὫὢὩὦὤὪὧὫὡ (v2026-04-11 기준)
// 게임 업데이트로 이름이 바뀌면 Exception을 catch해 로그 출력 후 무시한다.
//
// [패치 등록]
// [HarmonyPatch] 속성 없이 ApplyPatches()에서 수동 등록한다.
// ShowUI 오버로드가 여러 개이므로 파라미터 없는 버전을 명시적으로 선택한다.

using HarmonyLib;
using RayelleBX.Config;
using System;
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
            // ShowUI() — 파라미터 없는 오버로드 (Type[] {} 로 명시)
            MethodInfo target = AccessTools.Method(typeof(CharUI), "ShowUI", new System.Type[] { });
            if (target == null)
            {
                Plugin.Log.LogWarning("[CharUI] ShowUI() 메서드를 찾을 수 없음");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(CharUIEnablePatch), nameof(Postfix)));
            Plugin.Log.LogInfo("[CharUI] ShowUI() 패치 등록");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[CharUI] 패치 실패: {e.Message}");
        }
    }

    // __instance : 패치된 CharUI 인스턴스
    private static void Postfix(CharUI __instance)
    {
        try
        {
            // 코스튬 탭 하위의 CharCostumeUI 컴포넌트를 탐색
            Transform costumeTab = __instance.transform.Find("UIRoot/Mask/Tab - 2 - Costume");
            CharCostumeUI costumeUI = costumeTab?.GetComponent<CharCostumeUI>();
            if (costumeUI == null) return;

            // 난독화 필드: CharCostumeUI 내부의 코스튬 데이터 객체
            // 이름이 변경되면 null이 반환되고 아래 null 체크에서 조용히 종료
            object costumeData = typeof(CharCostumeUI)
                .GetField("ὩὠὬὣὥὮὦὢὩὧὭ", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(costumeUI);
            if (costumeData == null) return;

            // 난독화 프로퍼티: 코스튬 데이터 객체에서 CostumeID를 꺼낸다
            // Public + NonPublic 모두 시도 — 접근 제한자가 버전마다 다를 수 있음
            object costumeId = costumeData.GetType()
                .GetProperty("ὪὫὫὢὩὦὤὪὧὫὡ", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(costumeData);

            // 코스튬 탭의 TMP 텍스트에서 캐릭터명과 코스튬명을 읽는다
            // null 병합 연산자로 TMP 컴포넌트가 없거나 텍스트가 비어있는 경우를 처리
            string charName    = costumeTab.Find("ViewingTitle/StarLayout/Text - CharName - Viewing")
                                    ?.GetComponent<TextMeshProUGUI>()?.text ?? "UnknownChar";
            string costumeName = costumeTab.Find("ViewingTitle/Text - CostumeName - Viewing")
                                    ?.GetComponent<TextMeshProUGUI>()?.text ?? "UnknownCostume";
            string fullName = $"{charName}_{costumeName}";

            Plugin.Log.LogInfo($"[Costume Patch] CostumeID={costumeId}, Name={fullName}");
            // CSV에 한 줄 추가 — 중복 체크 없이 append (같은 코스튬을 여러 번 열면 여러 번 기록됨)
            CostumeConfig.AppendMapping(costumeId, fullName);
        }
        catch (Exception ex)
        {
            // 난독화 이름 변경 등으로 리플렉션이 실패해도 크래시 없이 로그만 출력
            Plugin.Log.LogInfo($"Exception in CharUIEnablePatch:\n{ex}");
        }
    }
}
