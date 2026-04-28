// DeckMessagePatch.cs — 서버에서 받은 덱 데이터로 코스튬 ID를 자동 기록하는 패치.
//
// [패치 대상]
// DeckMessageInfo.MergeFrom(DeckMessageInfo other)
// — 서버 응답(덱/인벤토리 갱신)을 로컬 인스턴스에 병합할 때 호출된다.
//   로그인 직후 전체 덱 수신, 가챠 후 코스튬 추가 등의 시점이 여기서 잡힌다.
//
// [동작]
// Postfix에서 __instance.CostumeInfo 를 순회해 소유 중인 코스튬 ID를 CSV에 기록한다.
// CostumeConfig.AppendMapping의 중복 체크로 이미 기록된 ID는 재기록하지 않는다.
// 이름 컬럼은 빈 문자열로 기록된다 — CharUIEnablePatch가 나중에 채울 수 있지만,
// UR 판정 로직(LoadAllCostumes)은 ID만 사용하므로 기능상 문제없다.

using HarmonyLib;
using RayelleBX.Config;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace RayelleBX.Patches;

public class DeckMessagePatch
{
    public static void ApplyPatches(Harmony harmony)
    {
        Type deckType = AccessTools.TypeByName("DeckMessageInfo");
        if (deckType == null)
        {
            Plugin.Log.LogWarning("[DeckMessage] DeckMessageInfo 타입을 찾을 수 없음");
            return;
        }

        // 경로 1: MergeFrom(CodedInputStream) — 구버전 Google.Protobuf 파싱 경로
        TryPatch(harmony, deckType, "MergeFrom",
            new[] { AccessTools.TypeByName("Google.Protobuf.CodedInputStream") });

        // 경로 2: IBufferMessage.InternalMergeFrom(ref ParseContext) — 신버전 Google.Protobuf 파싱 경로
        // 인터페이스 맵으로 구체 구현 메서드를 찾아 패치
        TryPatchInternalMergeFrom(harmony, deckType);
    }

    private static void TryPatch(Harmony harmony, Type declaringType, string methodName, Type[] paramTypes)
    {
        try
        {
            if (paramTypes == null || paramTypes.Any(t => t == null)) return;
            MethodInfo target = AccessTools.Method(declaringType, methodName, paramTypes);
            if (target == null) return;
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(DeckMessagePatch), nameof(Postfix)));
            Plugin.Log.LogInfo(
                $"[DeckMessage] {methodName}({target.GetParameters()[0].ParameterType.Name}) 패치 등록");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[DeckMessage] {methodName} 패치 실패: {e.Message}");
        }
    }

    private static void TryPatchInternalMergeFrom(Harmony harmony, Type deckType)
    {
        try
        {
            Type iBufferMsg = AccessTools.TypeByName("Google.Protobuf.IBufferMessage");
            if (iBufferMsg == null || !iBufferMsg.IsAssignableFrom(deckType)) return;

            InterfaceMapping map = deckType.GetInterfaceMap(iBufferMsg);
            for (int i = 0; i < map.InterfaceMethods.Length; i++)
            {
                if (!map.InterfaceMethods[i].Name.Contains("InternalMergeFrom")) continue;
                MethodInfo target = map.TargetMethods[i];
                harmony.Patch(target, postfix: new HarmonyMethod(typeof(DeckMessagePatch), nameof(Postfix)));
                Plugin.Log.LogInfo($"[DeckMessage] InternalMergeFrom 패치 등록");
                return;
            }
            Plugin.Log.LogWarning("[DeckMessage] IBufferMessage.InternalMergeFrom 구현체 없음");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[DeckMessage] InternalMergeFrom 패치 실패: {e.Message}");
        }
    }

    private static int _callCount = 0;

    private static void Postfix(object __instance)
    {
        try
        {
            int call = System.Threading.Interlocked.Increment(ref _callCount);

            PropertyInfo costumeInfoProp = __instance.GetType()
                .GetProperty("CostumeInfo", BindingFlags.Instance | BindingFlags.Public);
            if (costumeInfoProp == null) return;

            var costumeList = costumeInfoProp.GetValue(__instance) as IEnumerable;
            int total = 0;
            int added = 0;

            if (costumeList != null)
            {
                foreach (object costume in costumeList)
                {
                    PropertyInfo idProp = costume.GetType()
                        .GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
                    if (idProp == null) continue;

                    int id = (int)idProp.GetValue(costume);
                    if (id <= 0) continue;

                    total++;
                    if (CostumeConfig.AppendMapping(id, ""))
                        added++;
                }
            }

            // 처음 5회는 항상 로그, 이후 코스튬이 있을 때만 로그
            if (call <= 5 || total > 0)
                Plugin.Log.LogInfo($"[DeckMessage] 호출#{call} CostumeInfo={total}개, 신규={added}개");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogInfo($"[DeckMessage] Postfix 예외: {ex}");
        }
    }
}
