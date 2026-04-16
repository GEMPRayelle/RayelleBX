// UIHelper.cs — UI GameObject 탐색 및 버튼 클릭 호출 헬퍼.
//
// [배경]
// 매크로 패치들은 모두 GameObject.Find()로 UI 오브젝트를 찾고 Button.onClick을 invoke한다.
// 이 패턴이 여러 패치에서 반복되므로 공통 메서드로 묶었다.
// 실패 시 false를 반환해 호출부에서 break/continue로 매크로를 중단할 수 있다.

using UnityEngine;
using UnityEngine.UI;

namespace RayelleBX.Helpers;

public static class UIHelper
{
    /// <summary>
    /// 해당 경로의 GameObject가 존재하고 activeSelf인지 확인한다.
    /// 매크로 루프에서 다음 단계로 넘어가기 전 UI가 열렸는지 폴링할 때 사용한다.
    /// </summary>
    public static bool IsExistAndActive(string path, string label = null)
    {
        GameObject go = GameObject.Find(path);
        if (go == null) return false;
        return go.activeSelf;
    }

    /// <summary>
    /// 경로로 버튼을 찾아 onClick을 invoke한다.
    /// GameObject.Find 실패 또는 Button 컴포넌트 없으면 false 반환.
    /// </summary>
    public static bool TryInvokeButton(string path, string label = null)
    {
        if (label == null) label = path;
        GameObject go = GameObject.Find(path);
        if (go == null)
        {
            Plugin.Log.LogWarning($"[UIHelper] '{label}' Not Found");
            return false;
        }
        return TryInvokeButton(go, label);
    }

    /// <summary>
    /// GameObject에서 Button 컴포넌트를 꺼내 onClick을 invoke한다.
    /// 이미 오브젝트 참조가 있을 때 사용한다.
    /// </summary>
    public static bool TryInvokeButton(GameObject go, string label = null)
    {
        Button btn = go.GetComponent<Button>();
        if (btn == null)
        {
            Plugin.Log.LogWarning($"[UIHelper] Button component not found on '{label ?? go.name}'");
            return false;
        }
        //Plugin.Log.LogInfo($"[UIHelper] '{label ?? go.name}' clicked"); //Debugging
        btn.onClick.Invoke();
        return true;
    }

    /// <summary>
    /// GameObject.Find를 실행하고, 실패 시 로그를 남긴 뒤 null을 반환한다.
    /// 호출부에서 null 체크로 초기화 실패를 감지한다.
    /// </summary>
    public static GameObject FindOrLog(string path, string label = null)
    {
        GameObject go = GameObject.Find(path);
        if (go == null)
            Plugin.Log.LogWarning($"[UIHelper] '{label ?? path}' Not Found");
        return go;
    }
}
