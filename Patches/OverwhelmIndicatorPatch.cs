// OverwhelmIndicatorPatch.cs — 압도(Overwhelm) 스킬 발동 시 심볼 몬스터 방향 인디케이터를 표시하는 패치.
//
// [기능 개요]
// 플레이어가 압도 스킬을 사용하면 필드의 모든 "Symbol_" 몬스터를 향한 빨간 방향 화살표를 생성한다.
// 인디케이터는 스킬 지속 시간 동안 매 0.3초마다 갱신되며, 시간이 끝나면 자동 제거된다.
//
// [패치 대상: 난독화 문제]
// Assembly-CSharp.dll은 게임 업데이트마다 메서드명이 바뀌는 난독화를 적용한다.
// 대상 메서드는 TalentSkillManager의 압도 스킬 관련 메서드인데, 이름이 매번 달라진다.
// 해결책: 후보 이름 배열을 나열하고 실제로 존재하는 메서드만 패치한다.
// Plugin.cs에서 GetTargetMethods()를 직접 호출해 수동 패치하는 이유가 바로 이것이다.
//
// [인디케이터 생성 방식]
// 게임에 이미 존재하는 DirectionFieldMark0 (초록 화살표)를 복제해 빨간 화살표로 바꾼다.
// Init() 메서드의 3번째 파라미터가 난독화된 enum 타입이라 리플렉션으로 타입을 얻어 전달한다.
//
// [코루틴 구조]
// 유니티의 StartCoroutine을 사용한다. TalentSkillManager가 MonoBehaviour이므로 가능하다.
// 코루틴 안에서 activeIndicators 딕셔너리를 관리해 추가/제거를 동적으로 처리한다.

using HarmonyLib;
using Proto.Design.common;
using RayelleBX.Components;
using RayelleBX.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RayelleBX.Patches;

public class OverwhelmIndicatorPatch
{
    // 현재 활성화된 인디케이터를 몬스터 GameObject → DirectionFieldIndicator 로 추적한다.
    // 딕셔너리를 쓰는 이유: 몬스터가 도중에 제거될 수 있어 개별 추적이 필요하기 때문.
    private static Dictionary<GameObject, DirectionFieldIndicator> activeIndicators = new Dictionary<GameObject, DirectionFieldIndicator>();

    // 코루틴 중복 실행 방지 플래그.
    // 압도 스킬이 연속으로 발동돼도 코루틴이 하나만 실행되도록 보장한다.
    private static bool _isRunning = false;

    // 실행 중인 코루틴 참조와 호스트 MonoBehaviour.
    // ClearAndDestroyIndicators()에서 StopCoroutine으로 명시 중단하기 위해 보관한다.
    // 지역 이동 시 TalentSkillManager 인스턴스가 유지되면 이전 코루틴이 계속 살아있어
    // 새 코루틴의 activeIndicators와 _isRunning 플래그를 덮어쓰는 버그를 막기 위함이다.
    private static Coroutine _activeCoroutine = null;
    private static MonoBehaviour _coroutineHost = null;

    // 압도 버프 만료 시각 (Time.time 기준).
    // 마법진 등으로 필드를 이동하면 게임은 압도가 이미 활성 상태이므로 스킬 메서드를 재호출하지 않는다.
    // 이 값으로 버프가 아직 남아 있는지 판단해 새 필드 진입 시 코루틴을 자동 재시작한다.
    private static float _overwhelmEndTime = 0f;

    /// <summary>
    /// Harmony가 패치할 TalentSkillManager의 메서드 목록을 반환한다.
    /// 게임 업데이트마다 메서드 이름이 바뀌므로 후보를 배열로 나열하고,
    /// 실제로 존재하는 것만 yield return한다.
    /// Plugin.cs에서 이 메서드를 직접 호출해 수동 패치한다.
    /// </summary>
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

    /// <summary>
    /// TalentSkillManager의 압도 스킬 메서드 실행 직후 호출되는 Postfix.
    /// 스킬 지속 시간을 TalentSkillTable에서 읽어 코루틴을 시작한다.
    ///
    /// Harmony Postfix 파라미터 규칙:
    /// - __instance : 패치된 메서드의 this (TalentSkillManager 인스턴스)
    /// - __0, __1   : 원본 메서드의 첫 번째, 두 번째 파라미터 (이름 난독화로 순서 기반 참조)
    /// </summary>
    private static void Postfix(
        TalentSkillManager __instance,
        long __0,           // charInvenIndex — 캐릭터 인벤토리 인덱스 (현재 미사용)
        TalentSkillTable __1)   // talentTable — 스킬 데이터 테이블
    {
        try
        {
            // setting.cfg에서 비활성화됐으면 아무것도 하지 않는다
            if (!PluginConfig.OverwhelmIndicator) return;

            if (_isRunning)
            {
                // 코루틴이 이미 실행 중이어도 압도 스킬이 재사용되면 종료 시각을 갱신한다.
                // 갱신하지 않으면 코루틴이 이전 remaining 기준으로 먼저 끝나고,
                // 이후 RestartIfStillActive()가 _overwhelmEndTime을 기준으로 remaining을 계산할 때
                // 이미 만료된 것으로 판단해 재시작하지 않는 버그가 발생한다.
                float newDuration = 45f;
                if (__1 != null)
                {
                    FieldInfo f = typeof(TalentSkillTable).GetField("valueList_", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (f != null && f.GetValue(__1) is IList lst && lst.Count > 1)
                        newDuration = Convert.ToSingle(lst[1]);
                }
                _overwhelmEndTime = Time.time + newDuration;
                Plugin.Log.LogInfo($"[OverwhelmIndicatorPatch] 압도 재사용 — _overwhelmEndTime 갱신 (newDuration={newDuration})");
                return;
            }

            Plugin.Log.LogInfo("[OverwhelmIndicatorPatch] Postfix 호출됨");

            // 인디케이터를 붙일 부모 오브젝트 (필드 보상 UI 레이어)
            GameObject target = GameObject.Find("Singleton (DontDestroy)/AppManager/UI/GameFieldDefaultUI(Clone)/Parent/MapLayout/MapScaleParent/Layout - FieldReward");
            GameObject playerObj = GameObject.Find("GameFieldManager(Clone)/CharGroup/Player");
            if (playerObj == null)
            {
                Plugin.Log.LogWarning("[OverwhelmIndicatorPatch] Player 오브젝트를 찾을 수 없음");
                return;
            }

            Transform playerTransform = playerObj.transform;

            // TalentSkillTable의 valueList_ 필드에서 스킬 지속 시간을 읽는다.
            // valueList_[1]이 지속 시간(초)이다. 읽기 실패 시 기본값 45초를 사용한다.
            float duration = 45f;
            if (__1 != null)
            {
                FieldInfo field = typeof(TalentSkillTable).GetField("valueList_", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.GetValue(__1) is IList list && list.Count > 1)
                    duration = Convert.ToSingle(list[1]);
            }

            Plugin.Log.LogInfo($"[OverwhelmIndicatorPatch] 코루틴 시작 (duration={duration})");
            _overwhelmEndTime = Time.time + duration;
            _isRunning = true;
            _coroutineHost = __instance;
            // TalentSkillManager는 MonoBehaviour이므로 StartCoroutine을 직접 호출할 수 있다
            _activeCoroutine = __instance.StartCoroutine(UpdateDirectionRoutine(playerTransform, 0.3f, target));
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[OverwhelmIndicatorPatch] Postfix 예외: {e}");
            _isRunning = false;
        }
    }

    /// <summary>
    /// 모든 활성 인디케이터를 제거하고 상태를 초기화한다.
    /// 새 필드에 진입할 때 GameFieldDefaultUIEnablePatch에서 호출한다.
    ///
    /// 이전 코루틴을 명시적으로 StopCoroutine 한다.
    /// TalentSkillManager가 씬 전환 후에도 살아있을 경우,
    /// 이전 코루틴이 계속 실행되다가 duration 만료 시 _isRunning = false 및
    /// activeIndicators.Clear()를 덮어써 새 필드의 인디케이터를 망가뜨리는 버그를 방지한다.
    /// </summary>
    public static void ClearAndDestroyIndicators()
    {
        // 기존 코루틴을 명시적으로 중단
        if (_coroutineHost != null && _activeCoroutine != null)
        {
            try { _coroutineHost.StopCoroutine(_activeCoroutine); }
            catch (Exception) { /* 호스트가 이미 파괴된 경우 무시 */ }
        }
        _activeCoroutine = null;
        _coroutineHost = null;

        foreach (DirectionFieldIndicator indicator in activeIndicators.Values)
        {
            if (indicator != null && indicator.gameObject != null)
                UnityEngine.Object.Destroy(indicator.gameObject);
        }
        activeIndicators.Clear();
        _isRunning = false;
    }

    /// <summary>
    /// 새 필드 진입 시 압도 버프가 아직 활성 상태이면 코루틴을 재시작한다.
    /// GameFieldDefaultUIEnablePatch.Postfix()에서 ClearAndDestroyIndicators() 직후 호출한다.
    ///
    /// 마법진 등으로 필드를 이동하면 게임은 압도 스킬 메서드를 재호출하지 않는다.
    /// (버프가 이미 걸려 있어 중복 발동으로 처리하기 때문)
    /// _overwhelmEndTime과 Time.time을 비교해 남은 시간이 있으면 새 필드 기준으로 코루틴을 다시 건다.
    ///
    /// host(GameFieldDefaultUI)는 필드 로드 중 부모가 비활성화 상태일 수 있어
    /// StartCoroutine이 예외 없이 실패한다.
    /// CoroutineHelper.GetOrCreateRunner()는 DontDestroyOnLoad로 항상 활성화된 Runner이므로
    /// 코루틴이 확실히 실행된다.
    /// </summary>
    /// <param name="target">인디케이터 부모 오브젝트 (Layout - FieldReward)</param>
    public static void RestartIfStillActive(GameObject target)
    {
        if (!PluginConfig.OverwhelmIndicator) return;
        if (_isRunning) return;

        float remaining = _overwhelmEndTime - Time.time;
        if (remaining <= 0f) return;

        GameObject playerObj = GameObject.Find("GameFieldManager(Clone)/CharGroup/Player");
        if (playerObj == null) return;

        MonoBehaviour runner = CoroutineHelper.GetOrCreateRunner();
        Plugin.Log.LogInfo($"[OverwhelmIndicatorPatch] 필드 이동 후 압도 재시작 (remaining={remaining:F1}s)");
        _isRunning = true;
        _coroutineHost = runner;
        _activeCoroutine = runner.StartCoroutine(UpdateDirectionRoutine(playerObj.transform, 0.3f, target));
    }

    /// <summary>
    /// 압도 버프가 유효한 동안 interval 초마다 인디케이터를 갱신하는 코루틴.
    /// duration 파라미터 대신 _overwhelmEndTime을 직접 비교한다.
    /// 이렇게 하면 압도 스킬이 재사용되어 _overwhelmEndTime이 갱신되면
    /// 코루틴도 자동으로 연장된다.
    ///
    /// 매 tick마다:
    /// 1. FindObjectsOfType&lt;FieldMonsterController&gt;()로 현재 씬의 심볼 몬스터를 탐색
    ///    (FindObjectsOfType&lt;GameObject&gt;()는 씬의 모든 오브젝트를 순회해 매우 무거우므로 사용 금지)
    /// 2. 새로 발견된 심볼 몬스터 → 인디케이터 생성
    /// 3. 이미 등록된 몬스터 → UpdateDirection()으로 화살표 방향 갱신
    /// 4. 사라진 몬스터 → 인디케이터 제거
    /// </summary>
    private static IEnumerator UpdateDirectionRoutine(
        Transform playerTransform,
        float interval,     // 갱신 주기(초)
        GameObject target)  // 인디케이터를 붙일 부모 오브젝트 (필드 보상 UI 레이어)
    {
        Plugin.Log.LogInfo("[OverwhelmIndicatorPatch] 코루틴 진입");
        while (Time.time < _overwhelmEndTime)
        {
            try
            {
                if (playerTransform == null || playerTransform.gameObject == null)
                    break;

                if (target != null && !target.activeSelf)
                    target.SetActive(true);

                // 타입 필터링 검색 — GameObject 전수 탐색보다 훨씬 빠르다
                FieldMonsterController[] monsters = UnityEngine.Object.FindObjectsOfType<FieldMonsterController>();

                HashSet<GameObject> currentSymbols = new HashSet<GameObject>();

                foreach (FieldMonsterController monster in monsters)
                {
                    if (monster == null || monster.gameObject == null || !monster.gameObject.activeSelf) continue;
                    if (!SymbolMonsterHelper.IsSymbolMonster(monster.gameObject)) continue;

                    currentSymbols.Add(monster.gameObject);

                    // 새로운 심볼 몬스터면 인디케이터를 생성한다
                    if (!activeIndicators.ContainsKey(monster.gameObject))
                    {
                        // DirectionFieldMark0 : 게임이 이미 플레이어에게 붙여놓은 초록 방향 화살표 오브젝트
                        // 이걸 복제해서 빨간 화살표로 바꾸면 Init() 구현을 재사용할 수 있다
                        GameObject original = GameObject.Find("GameFieldManager(Clone)/CharGroup/Player/ItemDirectionParent/DirectionFieldMark0");
                        if (original != null)
                        {
                            // 원본과 같은 부모 아래에 복제 (same parent = 같은 월드 공간 유지)
                            GameObject clone = UnityEngine.Object.Instantiate(original, original.transform.parent);
                            clone.name = "DirectionFieldMark_Red_" + monster.name;
                            DirectionFieldIndicator indicator = clone.GetComponent<DirectionFieldIndicator>();
                            if (indicator != null && indicator.transform != null)
                            {
                                try
                                {
                                    // Init(playerTransform, target, colorEnum) 를 리플렉션으로 호출한다.
                                    // 3번째 파라미터의 enum 타입이 난독화되어 있어 직접 쓸 수 없다.
                                    // ParameterInfo에서 타입을 추출한 뒤 Enum.ToObject로 값(1)을 생성한다.
                                    MethodInfo initMethod = typeof(DirectionFieldIndicator).GetMethod("Init", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    if (initMethod != null)
                                    {
                                        ParameterInfo[] parameters = initMethod.GetParameters();
                                        if (parameters.Length >= 3)
                                        {
                                            Type enumType = parameters[2].ParameterType;
                                            object enumValue = Enum.ToObject(enumType, 1); // 1 = 빨간색 계열
                                            // FieldObjectBase로 캐스팅하지 않고 monster를 그대로 전달한다.
                                            // Init()의 파라미터 타입이 FieldObjectBase이므로 박싱된 상태에서 호환된다.
                                            initMethod.Invoke(indicator, new object[] { playerTransform, (FieldObjectBase)monster, enumValue });
                                        }
                                    }
                                    clone.SetActive(true);

                                    // 원본의 초록 화살표를 숨기고 빨간 화살표 크기를 조정한다
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

                    // 이미 등록된 인디케이터는 방향만 갱신
                    if (activeIndicators.TryGetValue(monster.gameObject, out DirectionFieldIndicator tmpindicator))
                    {
                        if (tmpindicator != null && tmpindicator.gameObject != null)
                        {
                            try { tmpindicator.UpdateDirection(); }
                            catch (Exception)
                            {
                                // UpdateDirection 실패 → 인디케이터 자체가 유효하지 않은 상태이므로 제거
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

                // currentSymbols에 없는 항목 = 이번 tick에서 사라진 몬스터 → 인디케이터 제거
                // 코루틴 안에서 딕셔너리를 직접 수정하면 예외가 발생하므로 toRemove 리스트를 별도로 만든다
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

            // interval 초 대기 후 다음 tick — WaitForSeconds는 유니티 코루틴 표준 대기 방식
            yield return new WaitForSeconds(interval);
        }

        // 지속 시간 종료 — 남아있는 인디케이터 전부 제거 및 플래그 초기화
        foreach (DirectionFieldIndicator indicator in activeIndicators.Values)
        {
            if (indicator != null && indicator.gameObject != null)
                UnityEngine.Object.Destroy(indicator.gameObject);
        }
        activeIndicators.Clear();
        _activeCoroutine = null;
        _coroutineHost = null;
        _isRunning = false;
    }
}
