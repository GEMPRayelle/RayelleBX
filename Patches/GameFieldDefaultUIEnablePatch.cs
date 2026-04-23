// GameFieldDefaultUIEnablePatch.cs — 필드 진입 시 심볼 몬스터 수 UI를 생성하는 패치.
//
// [UI 계층 구조]
// Layout - FieldReward
//   └─ RBX_SymbolCount          ← 이 패치가 생성 (게임 오브젝트와 이름 충돌 방지를 위해 고유 접두사 사용)
//        ├─ Image - Icon
//        └─ Text - Count

using BepInEx;
using HarmonyLib;
using RayelleBX.Helpers;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RayelleBX.Patches;

[HarmonyPatch(typeof(GameFieldDefaultUI), "LoadFieldComplete")]
public class GameFieldDefaultUIEnablePatch
{
    public const string ItemName = "RBX_SymbolCount";

    public static GameFieldDefaultUI ActiveFieldUI { get; private set; }

    private static void Prefix(GameFieldDefaultUI __instance)
    {
        Plugin.Log.LogInfo($"[FieldUI] LoadFieldComplete BEFORE — id={__instance.GetInstanceID()}, active={__instance.gameObject.activeSelf}");
    }

    private static void Postfix(GameFieldDefaultUI __instance)
    {
        try
        {
            int fieldUICount = UnityEngine.Object.FindObjectsOfType<GameFieldDefaultUI>().Length;
            Plugin.Log.LogInfo($"[FieldUI] LoadFieldComplete AFTER — id={__instance.GetInstanceID()}, active={__instance.gameObject.activeSelf}, fieldUICount={fieldUICount}");

            ActiveFieldUI = __instance;
            Transform fieldRewardT = __instance.transform.Find("Parent/MapLayout/MapScaleParent/Layout - FieldReward");
            if (fieldRewardT == null)
            {
                Plugin.Log.LogWarning("[GameFieldDefaultUIEnablePatch] Layout - FieldReward 오브젝트를 찾지 못함 → 종료");
                return;
            }
            GameObject fieldReward = fieldRewardT.gameObject;

            // 이전 실행에서 우리가 생성한 오브젝트가 남아있으면 제거
            Transform existingItem = fieldReward.transform.Find(ItemName);
            if (existingItem != null)
            {
                Plugin.Log.LogInfo($"[FieldUI] 기존 {ItemName} 제거");
                Object.Destroy(existingItem.gameObject);
            }

            int symbolCount = SymbolMonsterHelper.GetSymbolCount();
            Plugin.Log.LogInfo($"[FieldUI] symbolCount={symbolCount}");

            if (symbolCount > 0)
            {
                GameObject item = new GameObject(ItemName);
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
                TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.text = symbolCount.ToString();
                tmp.color = new Color(1f, 1f, 1f, 1f);
                tmp.fontSize = 26;
                tmp.alignment = TextAlignmentOptions.Center;
            }
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogError($"[GameFieldDefaultUIEnablePatch] 예외 발생: {e}");
        }
    }

}
