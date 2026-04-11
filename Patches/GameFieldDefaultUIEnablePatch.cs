using BepInEx;
using HarmonyLib;
using RayelleBX.Helpers;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace RayelleBX.Patches;

[HarmonyPatch(typeof(GameFieldDefaultUI), "LoadFieldComplete")]
public class GameFieldDefaultUIEnablePatch
{
    private static void Postfix(GameFieldDefaultUI __instance)
    {
        try
        {
            Plugin.Log.LogInfo("[GameFieldDefaultUIEnablePatch] Postfix 시작");
            OverwhelmIndicatorPatch.ClearAndDestroyIndicators();
            Plugin.Log.LogInfo("[GameFieldDefaultUIEnablePatch] ClearAndDestroyIndicators 완료");

            GameObject fieldReward = GameObject.Find("Singleton (DontDestroy)/AppManager/UI/GameFieldDefaultUI(Clone)/Parent/MapLayout/MapScaleParent/Layout - FieldReward");
            if (fieldReward == null)
            {
                Plugin.Log.LogInfo("[GameFieldDefaultUIEnablePatch] Layout - FieldReward 오브젝트를 찾지 못함 → 종료");
                return;
            }
            Plugin.Log.LogInfo("[GameFieldDefaultUIEnablePatch] fieldReward 찾음");

            Transform existingItem = fieldReward.transform.Find("Button - Item6");
            if (existingItem != null)
                Object.Destroy(existingItem.gameObject);

            int symbolCount = GetSymbolCount();
            Plugin.Log.LogInfo($"[GameFieldDefaultUIEnablePatch] symbolCount = {symbolCount}");

            if (symbolCount > 0)
            {
                if (!fieldReward.activeSelf)
                    fieldReward.SetActive(true);

                GameObject item = new GameObject("Button - Item6");
                item.transform.SetParent(fieldReward.transform, false);
                item.AddComponent<RectTransform>().sizeDelta = new Vector2(50f, 60f);
                item.AddComponent<CanvasRenderer>();
                LayoutElement layoutElement = item.AddComponent<LayoutElement>();
                layoutElement.minHeight = 60f;
                layoutElement.minWidth = 50f;

                GameObject iconGo = new GameObject("Image - Icon");
                iconGo.transform.SetParent(item.transform, false);
                RectTransform iconRect = iconGo.AddComponent<RectTransform>();
                iconRect.sizeDelta = new Vector2(45f, 45f);
                iconRect.anchoredPosition = new Vector2(0f, 8f);
                Image image = iconGo.AddComponent<Image>();

                string imagePath = Path.Combine(Paths.PluginPath, "RayelleBX", "Resources", "symbol_monster.png");
                if (File.Exists(imagePath))
                {
                    byte[] data = File.ReadAllBytes(imagePath);
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(data);
                    Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0U, SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f));
                    if (sprite != null)
                        image.sprite = sprite;
                }
                image.color = new Color(0.8706f, 0.8588f, 0.8275f, 1f);

                GameObject textGo = new GameObject("Text - Count");
                textGo.transform.SetParent(item.transform, false);
                RectTransform textRect = textGo.AddComponent<RectTransform>();
                textRect.sizeDelta = new Vector2(40f, 40f);
                textRect.anchoredPosition = new Vector2(0f, -10f);
                Plugin.Log.LogInfo("[GameFieldDefaultUIEnablePatch] CreateTMPro 호출 전");
                ComponentHelper.CreateTMPro(textGo, symbolCount.ToString(), new Color(1f, 1f, 1f, 1f), 26);
                Plugin.Log.LogInfo("[GameFieldDefaultUIEnablePatch] CreateTMPro 완료");
            }

            Plugin.Log.LogInfo("[GameFieldDefaultUIEnablePatch] Postfix 정상 완료");
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogError($"[GameFieldDefaultUIEnablePatch] 예외 발생: {e}");
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
