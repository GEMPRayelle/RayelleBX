// CoroutineRunner.cs — 매크로 코루틴을 실행하는 MonoBehaviour 컴포넌트.
//
// [역할]
// Harmony Postfix(static 메서드)는 StartCoroutine을 직접 호출할 수 없다.
// CoroutineHelper.GetOrCreateRunner()로 이 컴포넌트를 얻은 뒤
// runner.StartCoroutine(...)으로 매크로 루프를 실행한다.
//
// [Q키 중단]
// 매 프레임 Update()에서 Q키 입력을 감지한다.
// ComponentHelper.IsMacroRunning을 false로 바꾸면 각 매크로 코루틴의
// while(IsMacroRunning) 조건이 false가 되어 루프가 종료된다.

using RayelleBX.Helpers;
using UnityEngine;

namespace RayelleBX.Components;

public class CoroutineRunner : MonoBehaviour
{
    private void Update()
    {
        // 매크로 실행 중일 때만 Q키를 감지한다 (불필요한 조건 분기 최소화)
        if (Input.GetKeyDown(KeyCode.Q) && ComponentHelper.IsMacroRunning)
        {
            Plugin.Log.LogInfo("[CoroutineRunner] Q키 감지 → 매크로 중단");
            ComponentHelper.IsMacroRunning = false;
        }
    }
}
