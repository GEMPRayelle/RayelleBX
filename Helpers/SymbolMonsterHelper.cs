// SymbolMonsterHelper.cs — 심볼(목표) 몬스터 판별 및 탐색 공통 로직.
//
// [배경]
// 일반 필드의 심볼 몬스터는 "Symbol_" prefix를 사용한다.
// Memory's Edge 등 일부 특수 팩은 "FieldMonster_" prefix를 사용한다.
//
// [폴백 규칙]
// 씬에 "Symbol_" 몬스터가 하나라도 있으면 → Symbol_ 만 대상으로 삼는다.
// "Symbol_" 몬스터가 전혀 없으면 → "FieldMonster_" 를 대상으로 삼는다.
// 이렇게 하면 일반 필드에서 FieldMonster_ 잡몹에 화살표가 붙는 오염을 방지한다.

using System.Collections.Generic;
using UnityEngine;

namespace RayelleBX.Helpers;

public static class SymbolMonsterHelper
{
    private const string PrimaryPrefix = "Symbol_";
    private const string FallbackPrefix = "FieldMonster_";

    /// <summary>
    /// 현재 씬에서 활성화된 심볼(목표) 몬스터 GameObject를 반환한다.
    /// Symbol_ 이 없으면 FieldMonster_ 를 폴백으로 사용한다.
    /// </summary>
    public static List<GameObject> FindSymbolObjects()
    {
        List<GameObject> primary = new List<GameObject>();
        List<GameObject> fallback = new List<GameObject>();

        foreach (GameObject obj in Object.FindObjectsOfType<GameObject>())
        {
            if (!obj.activeSelf) continue;
            if (obj.name.StartsWith(PrimaryPrefix))
                primary.Add(obj);
            else if (obj.name.StartsWith(FallbackPrefix))
                fallback.Add(obj);
        }

        return primary.Count > 0 ? primary : fallback;
    }

    /// <summary>
    /// 현재 씬의 심볼(목표) 몬스터 수를 반환한다.
    /// </summary>
    public static int GetSymbolCount() => FindSymbolObjects().Count;

    /// <summary>
    /// 주어진 GameObject가 심볼(목표) 몬스터인지 판별한다.
    /// 씬 전체 탐색 없이 이름만으로 판단하므로 RemoveMonster Postfix 등에서 사용한다.
    /// 일반 필드에서는 Symbol_ 만 해당하므로 FieldMonster_ 는 폴백용으로만 체크하면 안 된다.
    /// 대신 씬 컨텍스트(Symbol_ 몬스터가 있는지)를 캐시해서 판단한다.
    /// </summary>
    public static bool IsSymbolMonster(GameObject go)
    {
        if (go == null) return false;
        if (go.name.StartsWith(PrimaryPrefix)) return true;
        // FieldMonster_ 는 Symbol_ 이 씬에 없을 때만 심볼로 취급한다
        if (go.name.StartsWith(FallbackPrefix))
        {
            foreach (GameObject obj in Object.FindObjectsOfType<GameObject>())
            {
                if (obj.activeSelf && obj.name.StartsWith(PrimaryPrefix))
                    return false; // Symbol_ 이 존재하므로 FieldMonster_ 는 일반 몬스터
            }
            return true;
        }
        return false;
    }
}
