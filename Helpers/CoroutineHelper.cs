// CoroutineHelper.cs — 매크로 코루틴을 실행할 CoroutineRunner를 가져오거나 생성한다.
//
// [배경: 왜 별도 Runner가 필요한가]
// Harmony Postfix는 일반 static 메서드이며 MonoBehaviour가 아니다.
// Unity의 StartCoroutine은 MonoBehaviour 인스턴스에서만 호출할 수 있기 때문에
// 코루틴을 시작하려면 씬에 살아있는 MonoBehaviour 오브젝트가 필요하다.
// 이 헬퍼는 "MacroCoroutineRunner"라는 전용 GameObject를 생성하고
// DontDestroyOnLoad로 씬 전환 후에도 유지되게 한다.

using RayelleBX.Components;
using UnityEngine;

namespace RayelleBX.Helpers;

public static class CoroutineHelper
{
    // Runner GameObject의 이름. 씬에서 Find()로 재사용할 수 있도록 고정 이름을 쓴다.
    private const string RunnerName = "MacroCoroutineRunner";

    /// <summary>
    /// "MacroCoroutineRunner" GameObject에서 CoroutineRunner 컴포넌트를 반환한다.
    /// 오브젝트나 컴포넌트가 없으면 새로 생성한다.
    /// DontDestroyOnLoad이므로 씬이 바뀌어도 Runner는 살아있다.
    /// </summary>
    public static CoroutineRunner GetOrCreateRunner()
    {
        GameObject target = GameObject.Find(RunnerName);
        if (target == null)
        {
            target = new GameObject(RunnerName);
            // 씬 전환 시 자동 파괴되지 않도록 설정 — 매크로 진행 중 씬이 바뀌어도 코루틴 유지
            Object.DontDestroyOnLoad(target);
        }
        // GetComponent가 null이면 (처음 생성된 경우) AddComponent로 붙인다
        return target.GetComponent<CoroutineRunner>() ?? target.AddComponent<CoroutineRunner>();
    }
}
