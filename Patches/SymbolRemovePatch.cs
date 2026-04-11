using HarmonyLib;
using TMPro;
using UnityEngine;

namespace RayelleBX.Patches;

[HarmonyPatch(typeof(FieldMonsterController), "RemoveMonster")]
public class SymbolRemovePatch
{
    private static void Postfix(FieldMonsterController __instance)
    {
        Plugin.Log.LogInfo("Symbol Removed");

        int symbolCount = GetSymbolCount();
        GameObject fieldReward = GameObject.Find("Singleton (DontDestroy)/AppManager/UI/GameFieldDefaultUI(Clone)/Parent/MapLayout/MapScaleParent/Layout - FieldReward");

        if (fieldReward == null) return;

        Transform item = fieldReward.transform.Find("Button - Item6");
        if (item == null) return;

        if (symbolCount > 0)
        {
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
            Object.Destroy(item.gameObject);
        }
    }

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
