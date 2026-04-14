# OverwhelmIndicator 버그 수정 기록

압도(Overwhelm) 인디케이터가 필드 이동 후 표시되지 않는 버그 3건의 원인 분석 및 수정 내용.

---

## 목차

1. [버그 1 — 구 코루틴 미중단으로 인한 상태 오염](#버그-1--구-코루틴-미중단으로-인한-상태-오염)
2. [버그 2 — 마법진 이동 후 압도 재발동 불가](#버그-2--마법진-이동-후-압도-재발동-불가)
3. [버그 3 — RestartIfStillActive 코루틴 미실행](#버그-3--restartifstillactive-코루틴-미실행)
4. [버그 4 — 흡수 스킬 사용 시 심볼 카운터 UI 오삭제](#버그-4--흡수-스킬-사용-시-심볼-카운터-ui-오삭제)
5. [버그 5 — 필드 이동 후 압도 재사용 시 화살표 조기 소멸](#버그-5--필드-이동-후-압도-재사용-시-화살표-조기-소멸)
6. [버그 6 — 일부 팩(Memory's Edge)에서 인디케이터 미표시](#버그-6--일부-팩memorys-edge에서-인디케이터-미표시)
7. [최종 상태 요약](#최종-상태-요약)

---

## 버그 1 — 구 코루틴 미중단으로 인한 상태 오염

### 증상

필드 A에서 압도를 사용한 뒤 다른 지역으로 이동하면 새 필드에서 인디케이터가 표시되지 않거나 도중에 사라진다.

### 원인

`ClearAndDestroyIndicators()`는 `_isRunning = false`와 딕셔너리 정리만 했고 **기존 코루틴을 명시적으로 중단하지 않았다**.

`TalentSkillManager`가 필드 전환 후에도 파괴되지 않고 유지되는 경우, 필드 A에서 시작된 코루틴이 계속 실행된다. 이 상태에서 필드 B에서 압도를 발동하면 두 코루틴이 동시에 실행되고, 구 코루틴의 duration이 만료되는 시점에 다음 두 가지를 덮어써버린다:

- `activeIndicators.Clear()` → 새 필드의 인디케이터 전부 삭제
- `_isRunning = false` → 타이밍에 따라 새 코루틴 플래그 오염

### 수정 내용 (`OverwhelmIndicatorPatch.cs`)

**추가된 필드**:
```csharp
private static Coroutine _activeCoroutine = null;
private static MonoBehaviour _coroutineHost = null;
```

**Postfix에서 코루틴 참조 저장**:
```csharp
_coroutineHost = __instance;
_activeCoroutine = __instance.StartCoroutine(UpdateDirectionRoutine(...));
```

**`ClearAndDestroyIndicators()`에서 명시적 중단**:
```csharp
if (_coroutineHost != null && _activeCoroutine != null)
{
    try { _coroutineHost.StopCoroutine(_activeCoroutine); }
    catch (Exception) { /* 호스트가 이미 파괴된 경우 무시 */ }
}
_activeCoroutine = null;
_coroutineHost = null;
```

---

## 버그 2 — 마법진 이동 후 압도 재발동 불가

### 증상

마법진(필드 이동 재능 스킬)을 가진 캐릭터의 스킬로 필드를 이동하면, 이동 후 새 필드에서 압도를 사용해도 인디케이터가 표시되지 않는다.

### 원인 파악 과정

BepInEx 로그에서 `RestartIfStillActive` 호출 이전에 `[DIAG]` 덤프를 추가해 `TalentSkillTable` 필드를 분석했다:

```
id_ = 5
groupId_ = 305
valueList_ (List) = [1, 60, 0]
talentSkillIconSpriteName_ = talent_bufficon_27_{0}
```

로그를 보면 **마법진 사용 시에는 `[DIAG]` 자체가 출력되지 않는다**. 마법진은 5개 후보 메서드 중 어느 것도 일치하지 않으므로, 잘못된 메서드 패치 문제가 아님이 확인됐다.

실제 원인: **게임은 압도 버프가 이미 활성 상태일 때 스킬 메서드를 재호출하지 않는다.** 따라서 마법진으로 필드를 이동하면:

1. `LoadFieldComplete` → `ClearAndDestroyIndicators()` → `_isRunning = false` ✓
2. 압도 버프는 여전히 활성 상태 (duration 60s 중 남은 시간 존재)
3. 새 필드에서 압도 사용 시도 → 게임이 이미 활성 스킬로 처리 → 메서드 미호출 → Postfix 미발동
4. `_isRunning = false`이지만 코루틴이 시작되지 않아 인디케이터 없음

### 수정 내용 (`OverwhelmIndicatorPatch.cs`)

**추가된 필드**:
```csharp
// 압도 버프 만료 시각 (Time.time 기준)
private static float _overwhelmEndTime = 0f;
```

**Postfix에서 만료 시각 기록**:
```csharp
_overwhelmEndTime = Time.time + duration;
```

**`RestartIfStillActive()` 추가**:
```csharp
public static void RestartIfStillActive(GameObject target)
{
    if (!PluginConfig.OverwhelmIndicator) return;
    if (_isRunning) return;

    float remaining = _overwhelmEndTime - Time.time;
    if (remaining <= 0f) return;

    GameObject playerObj = GameObject.Find("GameFieldManager(Clone)/CharGroup/Player");
    if (playerObj == null) return;

    MonoBehaviour runner = CoroutineHelper.GetOrCreateRunner();
    _isRunning = true;
    _coroutineHost = runner;
    _activeCoroutine = runner.StartCoroutine(
        UpdateDirectionRoutine(playerObj.transform, remaining, 0.3f, target));
}
```

**`GameFieldDefaultUIEnablePatch.Postfix()`에서 호출**:
```csharp
OverwhelmIndicatorPatch.ClearAndDestroyIndicators();
// ...fieldReward 탐색...
OverwhelmIndicatorPatch.RestartIfStillActive(fieldReward);
```

---

## 버그 3 — RestartIfStillActive 코루틴 미실행

### 증상

`RestartIfStillActive` 로그("필드 이동 후 압도 재시작 (remaining=XX.Xs)")는 찍히지만 인디케이터가 여전히 표시되지 않는다.

### 원인 파악

로그에서 핵심 단서: 직접 발동 시에는 "코루틴 진입" 로그가 나타나지만, `RestartIfStillActive`로 재시작할 경우 **"코루틴 진입"이 단 한 번도 출력되지 않는다.**

Unity에서 `StartCoroutine`을 비활성(`gameObject.activeInHierarchy == false`) MonoBehaviour에 호출하면 **예외 없이 조용히 무시된다.**

`RestartIfStillActive`는 `GameFieldDefaultUI` 인스턴스(`host`)에서 코루틴을 시작하는데, 필드 로딩 중에는 `GameFieldDefaultUI`의 부모 GameObject가 비활성 상태가 될 수 있다. 반면 직접 압도 발동 시에는 `TalentSkillManager`가 호스트로 사용되며, 이 매니저는 항상 활성 상태이므로 문제가 없었다.

```
직접 발동: TalentSkillManager.__instance  → 항상 active → 코루틴 정상 실행
RestartIfStillActive: GameFieldDefaultUI  → 로딩 중 inactive 가능 → StartCoroutine 무시
```

### 수정 내용 (`OverwhelmIndicatorPatch.cs`)

`CoroutineHelper.GetOrCreateRunner()`는 `DontDestroyOnLoad`로 항상 활성 상태가 보장된 `MacroCoroutineRunner` GameObject를 반환한다.

`host` 파라미터를 제거하고 Runner를 직접 사용하도록 변경:

```csharp
// 변경 전
public static void RestartIfStillActive(MonoBehaviour host, GameObject target)
{
    // ...
    _coroutineHost = host;
    _activeCoroutine = host.StartCoroutine(...);
}

// 변경 후
public static void RestartIfStillActive(GameObject target)
{
    // ...
    MonoBehaviour runner = CoroutineHelper.GetOrCreateRunner();
    _coroutineHost = runner;
    _activeCoroutine = runner.StartCoroutine(...);
}
```

**`GameFieldDefaultUIEnablePatch` 호출부도 수정**:
```csharp
// 변경 전
OverwhelmIndicatorPatch.RestartIfStillActive(__instance, fieldReward);

// 변경 후
OverwhelmIndicatorPatch.RestartIfStillActive(fieldReward);
```

---

---

## 버그 4 — 흡수 스킬 사용 시 심볼 카운터 UI 오삭제

### 증상

전투 필드에서 흡수 재능 스킬을 사용하면 심볼 몬스터가 남아있음에도 하단 심볼 카운터 UI(`Button - Item6`)가 사라진다.

### 원인

`SymbolRemovePatch`는 `FieldMonsterController.RemoveMonster`에 Postfix가 걸려 있어 **어떤 몬스터가 제거되든** 호출된다.

흡수 스킬이 일반 몬스터를 처치할 때 `RemoveMonster`가 호출되고, `GetSymbolCount()`를 실행한다. 이 시점에 심볼 몬스터가 스킬 이펙트로 인해 일시 비활성화(`activeSelf = false`) 상태이면 `FindObjectsOfType<GameObject>()`가 이를 탐색하지 못해 `count = 0`이 반환된다. 결과적으로 심볼이 모두 사라진 것으로 판단해 UI를 `Destroy`한다.

```
흡수 스킬 → 일반 몬스터 RemoveMonster 호출
  → SymbolRemovePatch.Postfix 실행
    → GetSymbolCount() 호출 (이 시점 심볼 몬스터 일시 비활성화)
      → count = 0 → Button - Item6 Destroy  ← 오삭제
```

### 수정 내용 (`SymbolRemovePatch.cs`)

제거된 몬스터가 심볼 몬스터가 아니면 조기 리턴하도록 수정:

```csharp
// 변경 전
private static void Postfix(FieldMonsterController __instance)
{
    Plugin.Log.LogInfo("Symbol Removed");
    int symbolCount = GetSymbolCount();

// 변경 후
private static void Postfix(FieldMonsterController __instance)
{
    if (__instance == null || __instance.gameObject == null) return;
    if (!SymbolMonsterHelper.IsSymbolMonster(__instance.gameObject)) return;

    Plugin.Log.LogInfo("Symbol Removed");
    int symbolCount = SymbolMonsterHelper.GetSymbolCount();
```

---

## 버그 5 — 필드 이동 후 압도 재사용 시 화살표 조기 소멸

### 증상

압도 버프가 남은 상태로 다른 필드로 이동해 압도 스킬을 **재사용**한 뒤 일정 시간이 지나면 화살표가 사라진다. 게임의 압도 버프는 아직 유효하지만 플러그인 로직상 종료 시각이 지난 것으로 처리된다.

### 원인

두 가지 문제가 복합 발생한다.

**문제 1 — `_overwhelmEndTime` 미갱신**

`Postfix`에서 `_isRunning = true`이면 조기 리턴하면서 `_overwhelmEndTime`을 갱신하지 않는다. 필드 이동 후 압도 스킬을 재사용해도 종료 시각이 처음 발동 시각 기준으로 고정된다.

**문제 2 — 코루틴이 고정 `duration`으로 실행**

`RestartIfStillActive()`는 남은 시간(`remaining`)을 계산해 코루틴에 `duration` 파라미터로 전달한다. 코루틴 내부는 `elapsed < duration`으로 종료를 판단하기 때문에, 이후 `_overwhelmEndTime`이 갱신되어도 코루틴은 원래 `remaining` 기준으로 조기 종료된다.

```
T=0:  압도 발동 → _overwhelmEndTime = 45, 코루틴 시작 (duration=45)
T=20: 필드 이동 → ClearAndDestroyIndicators, RestartIfStillActive
        → remaining=25, 새 코루틴 시작 (duration=25)
T=22: 필드 B에서 압도 재사용
        → _isRunning=true이므로 조기 리턴 ← _overwhelmEndTime 갱신 안 됨 (여전히 45)
T=45: 코루틴 elapsed=25 → duration=25 도달 → 코루틴 종료, _isRunning=false
        → 게임의 압도 버프는 T=67까지 유효하지만 화살표 소멸
```

### 수정 내용 (`OverwhelmIndicatorPatch.cs`)

**① `_isRunning = true`일 때도 `_overwhelmEndTime` 갱신**

```csharp
if (_isRunning)
{
    float newDuration = 45f;
    if (__1 != null)
    {
        FieldInfo f = typeof(TalentSkillTable).GetField("valueList_", ...);
        if (f != null && f.GetValue(__1) is IList lst && lst.Count > 1)
            newDuration = Convert.ToSingle(lst[1]);
    }
    _overwhelmEndTime = Time.time + newDuration;
    return;
}
```

**② 코루틴 종료 조건을 `elapsed < duration` → `Time.time < _overwhelmEndTime`으로 변경**

```csharp
// 변경 전
private static IEnumerator UpdateDirectionRoutine(
    Transform playerTransform, float duration, float interval, GameObject target)
{
    float elapsed = 0f;
    while (elapsed < duration)
    {
        // ...
        elapsed += interval;
    }
}

// 변경 후
private static IEnumerator UpdateDirectionRoutine(
    Transform playerTransform, float interval, GameObject target)
{
    while (Time.time < _overwhelmEndTime)
    {
        // ... (elapsed 변수 제거)
    }
}
```

`_overwhelmEndTime`이 갱신될 때 코루틴도 자동으로 연장된다. `RestartIfStillActive`의 `remaining` 파라미터도 제거됐다.

---

## 버그 6 — 일부 팩(Memory's Edge)에서 인디케이터 미표시

### 증상

Memory's Edge 등 일부 특수 팩에서 압도 스킬을 사용해도 방향 인디케이터가 표시되지 않고, 심볼 카운터 UI도 뜨지 않는다.

### 원인 파악

진단 로그를 추가해 코루틴 첫 tick에서 씬의 `FieldMonsterController` 이름을 전부 출력했다:

```
[OverwhelmIndicatorPatch] 몬스터 발견: "FieldMonster_101(청소부)" (active=True)
[OverwhelmIndicatorPatch] 몬스터 발견: "FieldMonster_102(청소부)" (active=True)
```

Memory's Edge는 심볼 몬스터 이름이 `Symbol_` 이 아닌 **`FieldMonster_`** prefix를 사용한다. 기존 코드는 `Symbol_`로만 필터링했으므로 아무것도 탐지하지 못했다.

일반 필드에서는 `FieldMonster_` = 일반 잡몹이므로, 단순히 `FieldMonster_`를 추가하면 일반 필드에서 잡몹에도 화살표가 붙는 오염이 발생한다.

### 수정 내용 — `Helpers/SymbolMonsterHelper.cs` 신규 추가

폴백 규칙을 공통 헬퍼로 분리:

- **`Symbol_` 몬스터가 씬에 하나라도 있으면** → `Symbol_` 만 대상 (일반 필드)
- **`Symbol_` 몬스터가 전혀 없으면** → `FieldMonster_` 를 대상 (Memory's Edge 등 특수 팩)

```csharp
public static bool IsSymbolMonster(GameObject go)
{
    if (go.name.StartsWith("Symbol_")) return true;
    if (go.name.StartsWith("FieldMonster_"))
    {
        // Symbol_ 이 씬에 존재하면 FieldMonster_ 는 일반 잡몹 취급
        foreach (GameObject obj in Object.FindObjectsOfType<GameObject>())
            if (obj.activeSelf && obj.name.StartsWith("Symbol_"))
                return false;
        return true;
    }
    return false;
}
```

세 곳의 인라인 `GetSymbolCount()` / `Symbol_` 직접 비교를 모두 `SymbolMonsterHelper`로 교체:

| 파일 | 변경 내용 |
|------|-----------|
| `OverwhelmIndicatorPatch.cs` | `monster.name.StartsWith("Symbol_")` → `SymbolMonsterHelper.IsSymbolMonster()` |
| `SymbolRemovePatch.cs` | 인라인 `GetSymbolCount()` 제거, `SymbolMonsterHelper` 사용 |
| `GameFieldDefaultUIEnablePatch.cs` | 인라인 `GetSymbolCount()` 제거, `SymbolMonsterHelper.GetSymbolCount()` 사용 |

`RayelleBX.csproj`에 `Helpers\SymbolMonsterHelper.cs` `<Compile>` 항목 추가.

---

## 최종 상태 요약

### 추가된 static 필드

| 필드 | 타입 | 역할 |
|------|------|------|
| `_activeCoroutine` | `Coroutine` | 실행 중인 코루틴 참조 (명시적 중단용) |
| `_coroutineHost` | `MonoBehaviour` | 코루틴 호스트 (StopCoroutine 대상) |
| `_overwhelmEndTime` | `float` | 압도 버프 만료 시각 (Time.time 기준) |

### 동작 흐름 (수정 후)

```
필드 A
  └─ 압도 발동
       ├─ _overwhelmEndTime = Time.time + 60
       ├─ _isRunning = true
       └─ TalentSkillManager에서 코루틴 시작 → 인디케이터 표시

마법진으로 필드 B 이동
  └─ LoadFieldComplete
       ├─ ClearAndDestroyIndicators()
       │    ├─ StopCoroutine(_activeCoroutine)  ← 구 코루틴 명시 중단
       │    ├─ 인디케이터 전부 Destroy
       │    └─ _isRunning = false
       └─ RestartIfStillActive(fieldReward)
            ├─ remaining = _overwhelmEndTime - Time.time  (예: 54.9s)
            ├─ CoroutineHelper.GetOrCreateRunner() 사용  ← 항상 active 보장
            └─ 새 코루틴 시작 → 인디케이터 표시
```

### 변경 파일

| 파일 | 변경 내용 |
|------|-----------|
| `Patches/OverwhelmIndicatorPatch.cs` | `_activeCoroutine`, `_coroutineHost`, `_overwhelmEndTime` 필드 추가; `ClearAndDestroyIndicators()` 명시적 StopCoroutine; `RestartIfStillActive()` 신규 추가; DIAG 로그 제거 |
| `Patches/GameFieldDefaultUIEnablePatch.cs` | `RestartIfStillActive(fieldReward)` 호출 추가 |
