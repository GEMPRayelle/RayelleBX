using HarmonyLib;
using Proto.Design.common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RayelleBX.Patches;

public class OverwhelmIndicatorPatch
{
    private static Dictionary<GameObject, DirectionFieldIndicator> activeIndicators = new Dictionary<GameObject, DirectionFieldIndicator>();
    private static bool _isRunning = false;

    // 게임 업데이트마다 메서드 이름이 바뀌므로 동일한 state machine을 쓰는 후보 전부 패치
    public static IEnumerable<MethodBase> GetTargetMethods()
    {
        string[] candidates = new[]
        {
            "ὬὣὥὩὧὬὭὣὡὬὦ",
            "ὯὯὥὬὡὠὨὢὬὡὧ",
            "ὤὨὡὣὢὤὧὦὪὯὢ",
            "ὤὨὡὬὫὥὥὬὬὨὦ",
            "ὦὤὡὪὢὡὤὪὯὭὦ",
        };

        foreach (string name in candidates)
        {
            MethodBase m = AccessTools.DeclaredMethod(typeof(TalentSkillManager), name);
            if (m != null)
            {
                Plugin.Log.LogInfo($"[OverwhelmIndicatorPatch] 패치 대상 등록: {name}");
                yield return m;
            }
            else
            {
                Plugin.Log.LogWarning($"[OverwhelmIndicatorPatch] 메서드 없음 (업데이트됨?): {name}");
            }
        }
    }

    private static void Postfix(
        TalentSkillManager __instance,
        long __0,
        TalentSkillTable __1)
    {
        try
        {
            if (_isRunning) return;

            Plugin.Log.LogInfo("[OverwhelmIndicatorPatch] Postfix 호출됨");

            GameObject target = GameObject.Find("Singleton (DontDestroy)/AppManager/UI/GameFieldDefaultUI(Clone)/Parent/MapLayout/MapScaleParent/Layout - FieldReward");
            GameObject playerObj = GameObject.Find("GameFieldManager(Clone)/CharGroup/Player");
            if (playerObj == null)
            {
                Plugin.Log.LogWarning("[OverwhelmIndicatorPatch] Player 오브젝트를 찾을 수 없음");
                return;
            }

            Transform playerTransform = playerObj.transform;
            float duration = 45f;
            if (__1 != null)
            {
                FieldInfo field = typeof(TalentSkillTable).GetField("valueList_", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.GetValue(__1) is IList list && list.Count > 1)
                    duration = Convert.ToSingle(list[1]);
            }

            Plugin.Log.LogInfo($"[OverwhelmIndicatorPatch] 코루틴 시작 (duration={duration})");
            _isRunning = true;
            __instance.StartCoroutine(UpdateDirectionRoutine(playerTransform, duration, 0.3f, target));
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[OverwhelmIndicatorPatch] Postfix 예외: {e}");
            _isRunning = false;
        }
    }

    public static void ClearAndDestroyIndicators()
    {
        foreach (DirectionFieldIndicator indicator in activeIndicators.Values)
        {
            if (indicator != null && indicator.gameObject != null)
                UnityEngine.Object.Destroy(indicator.gameObject);
        }
        activeIndicators.Clear();
        _isRunning = false;
    }

    private static IEnumerator UpdateDirectionRoutine(
        Transform playerTransform,
        float duration,
        float interval,
        GameObject target)
    {
        Plugin.Log.LogInfo("[OverwhelmIndicatorPatch] 코루틴 진입");
        float elapsed = 0f;
        while (elapsed < duration)
        {
            try
            {
                if (playerTransform == null || playerTransform.gameObject == null)
                    break;

                if (target != null && !target.activeSelf)
                    target.SetActive(true);

                // FindObjectsOfType<FieldMonsterController>()로 교체 — 전체 GameObject 탐색보다 훨씬 효율적
                FieldMonsterController[] monsters = UnityEngine.Object.FindObjectsOfType<FieldMonsterController>();
                HashSet<GameObject> currentSymbols = new HashSet<GameObject>();

                foreach (FieldMonsterController monster in monsters)
                {
                    if (monster == null || monster.gameObject == null || !monster.gameObject.activeSelf) continue;
                    if (!monster.gameObject.name.StartsWith("Symbol_")) continue;

                    currentSymbols.Add(monster.gameObject);

                    if (!activeIndicators.ContainsKey(monster.gameObject))
                    {
                        GameObject original = GameObject.Find("GameFieldManager(Clone)/CharGroup/Player/ItemDirectionParent/DirectionFieldMark0");
                        if (original != null)
                        {
                            GameObject clone = UnityEngine.Object.Instantiate(original, original.transform.parent);
                            clone.name = "DirectionFieldMark_Red_" + monster.name;
                            DirectionFieldIndicator indicator = clone.GetComponent<DirectionFieldIndicator>();
                            if (indicator != null && indicator.transform != null)
                            {
                                try
                                {
                                    MethodInfo initMethod = typeof(DirectionFieldIndicator).GetMethod("Init", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    if (initMethod != null)
                                    {
                                        ParameterInfo[] parameters = initMethod.GetParameters();
                                        if (parameters.Length >= 3)
                                        {
                                            Type enumType = parameters[2].ParameterType;
                                            object enumValue = Enum.ToObject(enumType, 1);
                                            initMethod.Invoke(indicator, new object[] { playerTransform, (FieldObjectBase)monster, enumValue });
                                        }
                                    }
                                    clone.SetActive(true);

                                    Transform greenArrow = clone.transform.Find("GreenArrow");
                                    if (greenArrow != null)
                                        greenArrow.gameObject.SetActive(false);

                                    Transform redArrow = clone.transform.Find("RedArrow");
                                    if (redArrow != null)
                                        redArrow.localScale = new Vector3(0.05f, 0.05f, 0.17f);

                                    activeIndicators[monster.gameObject] = indicator;
                                }
                                catch (Exception e)
                                {
                                    Plugin.Log.LogError($"[OverwhelmIndicatorPatch] 인디케이터 Init 실패: {e}");
                                    UnityEngine.Object.Destroy(clone);
                                }
                            }
                            else
                            {
                                UnityEngine.Object.Destroy(clone);
                            }
                        }
                    }

                    if (activeIndicators.TryGetValue(monster.gameObject, out DirectionFieldIndicator tmpindicator))
                    {
                        if (tmpindicator != null && tmpindicator.gameObject != null)
                        {
                            try { tmpindicator.UpdateDirection(); }
                            catch (Exception)
                            {
                                UnityEngine.Object.Destroy(tmpindicator.gameObject);
                                activeIndicators.Remove(monster.gameObject);
                            }
                        }
                        else
                        {
                            activeIndicators.Remove(monster.gameObject);
                        }
                    }
                }

                List<GameObject> toRemove = new List<GameObject>();
                foreach (KeyValuePair<GameObject, DirectionFieldIndicator> kv in activeIndicators)
                {
                    if (kv.Key == null || kv.Value == null || !currentSymbols.Contains(kv.Key))
                    {
                        if (kv.Value != null && kv.Value.gameObject != null)
                            UnityEngine.Object.Destroy(kv.Value.gameObject);
                        toRemove.Add(kv.Key);
                    }
                }
                foreach (GameObject key in toRemove)
                    activeIndicators.Remove(key);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[OverwhelmIndicatorPatch] 코루틴 루프 예외: {e}");
            }

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        foreach (DirectionFieldIndicator indicator in activeIndicators.Values)
        {
            if (indicator != null && indicator.gameObject != null)
                UnityEngine.Object.Destroy(indicator.gameObject);
        }
        activeIndicators.Clear();
        _isRunning = false;
    }
}
