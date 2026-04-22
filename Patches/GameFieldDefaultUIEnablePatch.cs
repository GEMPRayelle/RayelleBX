// GameFieldDefaultUIEnablePatch.cs — 필드 진입 시 심볼 몬스터 수 UI를 생성하는 패치.
//
// [패치 대상]
// GameFieldDefaultUI.LoadFieldComplete() — 전투 필드가 완전히 로드된 직후 호출되는 메서드.
// 이 시점에 필드의 몬스터 배치가 완료되어 있어 심볼 몬스터 수를 정확히 셀 수 있다.
//
// [동작 흐름]
// 1. Layout - FieldReward GameObject를 찾아 커스텀 아이템("Button - Item6") 추가
// 2. 아이콘 이미지(symbol_monster.png) + 카운트 텍스트(TMP)로 UI 구성
//
// [UI 계층 구조]
// Layout - FieldReward
//   └─ Button - Item6          ← 이 패치가 생성
//        ├─ Image - Icon        (심볼 몬스터 아이콘 이미지)
//        └─ Text - Count        (현재 심볼 몬스터 수, TextMeshProUGUI)

using BepInEx;
using HarmonyLib;
using RayelleBX.Helpers;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace RayelleBX.Patches;

// [HarmonyPatch] 어트리뷰트로 패치 대상 타입과 메서드명을 선언한다.
// Plugin.cs의 PatchAll()이 이 어트리뷰트를 읽어 자동으로 패치를 등록한다.
[HarmonyPatch(typeof(GameFieldDefaultUI), "LoadFieldComplete")]
public class GameFieldDefaultUIEnablePatch
{
    // Postfix 메서드명은 규칙이다. Harmony가 자동으로 인식한다.
    // __instance : 패치된 메서드의 this에 해당하는 GameFieldDefaultUI 인스턴스
    private static void Postfix(GameFieldDefaultUI __instance)
    {
        try
        {
            // 전체 경로로 UI 오브젝트를 찾는다.
            // GameObject.Find()는 성능이 좋지 않지만, 필드 로드 시 한 번만 실행되므로 문제없다.
            GameObject fieldReward = GameObject.Find("Singleton (DontDestroy)/AppManager/UI/GameFieldDefaultUI(Clone)/Parent/MapLayout/MapScaleParent/Layout - FieldReward");
            if (fieldReward == null)
            {
                Plugin.Log.LogWarning("[GameFieldDefaultUIEnablePatch] Layout - FieldReward 오브젝트를 찾지 못함 → 종료");
                return;
            }

            // 이전 실행에서 생성한 아이템이 남아있으면 제거 후 새로 만든다
            Transform existingItem = fieldReward.transform.Find("Button - Item6");
            if (existingItem != null)
                Object.Destroy(existingItem.gameObject);

            int symbolCount = SymbolMonsterHelper.GetSymbolCount();

            if (symbolCount > 0)
            {
                // fieldReward가 비활성화 상태면 표시되지 않으므로 강제 활성화
                if (!fieldReward.activeSelf)
                    fieldReward.SetActive(true);

                // 루트 아이템 GameObject — LayoutGroup의 자식으로 배치된다
                GameObject item = new GameObject("Button - Item6");
                item.transform.SetParent(fieldReward.transform, false);
                item.AddComponent<RectTransform>().sizeDelta = new Vector2(50f, 60f);
                item.AddComponent<CanvasRenderer>();
                LayoutElement layoutElement = item.AddComponent<LayoutElement>();
                layoutElement.minHeight = 60f;
                layoutElement.minWidth = 50f;

                // 아이콘 이미지 — PNG 파일을 런타임에 로드해 Sprite로 변환
                GameObject iconGo = new GameObject("Image - Icon");
                iconGo.transform.SetParent(item.transform, false);
                RectTransform iconRect = iconGo.AddComponent<RectTransform>();
                iconRect.sizeDelta = new Vector2(45f, 45f);
                iconRect.anchoredPosition = new Vector2(0f, 8f);
                Image image = iconGo.AddComponent<Image>();

                // Paths.PluginPath : BepInEx가 제공하는 plugins 폴더 절대 경로
                string imagePath = Path.Combine(Paths.PluginPath, "RayelleBX", "Resources", "symbol_monster.png");
                if (File.Exists(imagePath))
                {
                    byte[] data = File.ReadAllBytes(imagePath);
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(data); // PNG/JPG 바이트 배열을 Texture2D로 디코딩
                    // Sprite.Create : Texture2D의 일부 영역을 Sprite로 잘라낸다
                    // 9-슬라이스 border(24,24,24,24)를 지정해 UI 확대 시 모서리가 뭉개지지 않게 한다
                    Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0U, SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f));
                    if (sprite != null)
                        image.sprite = sprite;
                }
                image.color = new Color(0.8706f, 0.8588f, 0.8275f, 1f);

                // 카운트 텍스트 — ComponentHelper.CreateTMPro()로 TMP + FontLocalizer를 함께 생성
                GameObject textGo = new GameObject("Text - Count");
                textGo.transform.SetParent(item.transform, false);
                RectTransform textRect = textGo.AddComponent<RectTransform>();
                textRect.sizeDelta = new Vector2(40f, 40f);
                textRect.anchoredPosition = new Vector2(0f, -10f);
                ComponentHelper.CreateTMPro(textGo, symbolCount.ToString(), new Color(1f, 1f, 1f, 1f), 26);
            }
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogError($"[GameFieldDefaultUIEnablePatch] 예외 발생: {e}");
        }
    }

}
