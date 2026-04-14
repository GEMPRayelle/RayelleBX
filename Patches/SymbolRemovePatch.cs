// SymbolRemovePatch.cs — 심볼 몬스터 제거 시 카운터 UI를 실시간 업데이트하는 패치.
//
// [패치 대상]
// FieldMonsterController.RemoveMonster() — 몬스터가 필드에서 제거될 때 호출되는 메서드.
// 심볼 몬스터도 이 메서드를 통해 제거되므로 여기서 카운터를 갱신한다.
//
// [동작 흐름]
// 1. 남은 심볼 몬스터 수를 다시 센다
// 2. 0보다 크면 → Text - Count 텍스트만 갱신
// 3. 0이면 → Button - Item6 전체를 제거 (UI 정리)
//
// [GameFieldDefaultUIEnablePatch와의 관계]
// GameFieldDefaultUIEnablePatch : 필드 진입 시 UI를 '생성'
// SymbolRemovePatch             : 몬스터 제거될 때마다 UI를 '갱신 또는 삭제'

using HarmonyLib;
using TMPro;
using UnityEngine;

namespace RayelleBX.Patches;

[HarmonyPatch(typeof(FieldMonsterController), "RemoveMonster")]
public class SymbolRemovePatch
{
    // __instance : RemoveMonster()를 호출한 FieldMonsterController 인스턴스.
    // 어떤 몬스터가 제거됐는지 알 수 있지만, 현재는 전체 카운트만 사용한다.
    private static void Postfix(FieldMonsterController __instance)
    {
        // 심볼 몬스터가 아니라면 카운터 갱신 불필요.
        // 흡수 스킬 등으로 일반 몬스터가 제거될 때도 이 Postfix가 호출되며,
        // 그 시점에 심볼 몬스터가 일시 비활성화 상태이면 GetSymbolCount() == 0이 되어
        // UI가 잘못 삭제되는 버그를 방지한다.
        if (__instance == null || __instance.gameObject == null) return;
        if (!__instance.gameObject.name.StartsWith("Symbol_")) return;

        Plugin.Log.LogInfo("Symbol Removed");

        int symbolCount = GetSymbolCount();
        GameObject fieldReward = GameObject.Find("Singleton (DontDestroy)/AppManager/UI/GameFieldDefaultUI(Clone)/Parent/MapLayout/MapScaleParent/Layout - FieldReward");

        if (fieldReward == null) return;

        // 이 패치가 관리하는 아이템 슬롯
        Transform item = fieldReward.transform.Find("Button - Item6");
        if (item == null) return;

        if (symbolCount > 0)
        {
            // 심볼이 아직 남아 있으면 숫자만 바꾼다
            Transform textTransform = item.Find("Text - Count");
            if (textTransform != null)
            {
                TextMeshProUGUI tmp = textTransform.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                    tmp.text = symbolCount.ToString();
            }
        }
        else
        {
            // 심볼이 모두 제거됐으면 UI 아이템 자체를 없앤다
            Object.Destroy(item.gameObject);
        }
    }

    /// <summary>
    /// 현재 씬에서 이름이 "Symbol_"로 시작하는 활성 GameObject 수를 반환한다.
    /// GameFieldDefaultUIEnablePatch의 동일 메서드와 중복이지만,
    /// 각 패치가 독립적으로 동작할 수 있도록 각자 유지한다.
    /// </summary>
    private static int GetSymbolCount()
    {
        int count = 0;
        foreach (GameObject obj in Object.FindObjectsOfType<GameObject>())
        {
            if (obj.name.StartsWith("Symbol_"))
                count++;
        }
        return count;
    }
}
