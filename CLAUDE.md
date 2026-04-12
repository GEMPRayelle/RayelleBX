# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

BrownDust 2 게임용 BepInEx 5 플러그인. HarmonyLib을 이용해 게임 메서드를 런타임에 패치하여 추가 기능을 제공한다.

- **게임 경로**: `C:\Neowiz\Browndust2\Browndust2_10000001`
- **BepInEx core**: `...\BepInEx\core\`
- **게임 Managed DLL**: `...\BrownDust II_Data\Managed\`
- **플러그인 배포 경로**: `...\BepInEx\plugins\RayelleBX\`
- **BepInEx 로그**: `...\BepInEx\LogOutput.log`

## 빌드 및 배포

```
# Visual Studio 또는 MSBuild로 빌드 (빌드 완료 시 자동 배포)
msbuild RayelleBX.csproj /p:Configuration=Debug

# 배포 경로 확인
C:\Neowiz\Browndust2\Browndust2_10000001\BepInEx\plugins\RayelleBX\RayelleBX.dll
```

빌드 후 `DeployPlugin` MSBuild Target이 자동으로 DLL을 플러그인 폴더에 복사한다. 게임을 재실행하면 플러그인이 로드된다.

**프로젝트 설정 주의사항**:
- `TargetFrameworkVersion`: v4.8 (v4.0은 참조 어셈블리 없음)
- `LangVersion`: latest (`RefSafetyRulesAttribute` 컴파일에 필요)
- `UnityEngine.dll`을 별도 참조해야 함 (`BaseUnityPlugin` → `MonoBehaviour` 의존)

## 아키텍처

### Harmony 패치 타입

Harmony는 런타임에 타겟 메서드의 IL 코드 앞/뒤에 코드를 삽입한다. 이 프로젝트는 Postfix만 사용한다.

| 타입 | 실행 시점 | 주요 용도 |
|------|----------|----------|
| Prefix | 원본 메서드 실행 **전** | 실행 차단, 파라미터 변경 |
| **Postfix** | 원본 메서드 실행 **후** | 반환값·상태 읽기/수정 ← 이 프로젝트 |
| Transpiler | 원본 IL 자체 변경 | 고급 패치 |

Postfix 파라미터 규칙:
- `__instance` : 패치된 메서드의 `this`
- `__result` : 반환값 (ref로 수정 가능)
- `__0`, `__1`, ... : 원본 메서드의 파라미터 (이름 난독화 시 순서 기반 참조)

### 패치 등록 방식

`Plugin.cs`의 `Awake()`에서 두 가지 방식으로 패치를 등록한다:

1. **`[HarmonyPatch]` 속성 방식** — `GameFieldDefaultUIEnablePatch`, `SymbolRemovePatch`, `QuickMenuUIEnablePatch`, `CharRecoveryUIEnablePatch`, `CharUIEnablePatch`는 클래스에 속성이 있으므로 `_harmony.PatchAll(typeof(...))` 사용
2. **수동 패치 방식** — `OverwhelmIndicatorPatch`는 `[HarmonyPatch]` 속성 없이 `GetTargetMethods()`로 런타임에 대상을 탐색한 뒤 `_harmony.Patch(method, postfix: postfix)` 직접 호출

> **중요**: `OverwhelmIndicatorPatch`에 `TargetMethods()` (HarmonyX 자동 감지 이름)를 쓰면 `PatchAll` 시 이중 호출된다. 반드시 `GetTargetMethods()`로 이름을 유지하고 `Plugin.cs`에서 수동 패치할 것.

### Obfuscated 메서드 처리

`Assembly-CSharp.dll`은 난독화되어 있어 메서드/필드명이 게임 업데이트마다 변경된다.

- **TalentSkillManager 패치 후보**: 게임 업데이트 시 이름이 바뀌므로 `OverwhelmIndicatorPatch.GetTargetMethods()`에 후보 이름을 배열로 나열 — 존재하는 것만 패치, 없는 것은 Warning 로그
- **FontLocalizer 필드** (v2026-04-11 기준): `ὡὥὢὬὡὭὯὭὥὦὢ` (fontName), `ὮὯὡὨὬὯὭὬὯὫὫ` (fontMaterial), apply메서드: `ὤὮὫὯὦὭὥὦὫὩὢ` — 업데이트로 변경 시 Warning 로그 출력 (크래시는 방지됨). Awake()가 호출하는 메서드 중 `_textTarget + fontName + fontMaterial`을 모두 참조하는 것이 apply 메서드
- obfuscated 이름 탐색은 Mono.Cecil로 `Assembly-CSharp.dll` 분석

새 이름 탐색 예시 (Mono.Cecil):
```csharp
// 동일한 state machine 타입(ὪὤὮὥὬὮὠὦὦὫὧ)을 반환하는 TalentSkillManager 메서드 탐색
// 필드: charInvenIndex(long), talentTable(TalentSkillTable), durationData
```

### 주요 기능

**OverwhelmIndicator** (`Patches/OverwhelmIndicatorPatch.cs`)
- `TalentSkillManager`의 압도 스킬 메서드 Postfix로 트리거
- 필드 내 `Symbol_` 이름의 `FieldMonsterController` 오브젝트를 탐색
- `DirectionFieldMark0`을 복제해 빨간 방향 인디케이터 생성 (`Init()` 리플렉션 호출)
- `_isRunning` 플래그로 중복 실행 방지 (압도 스킬 연속 발동 시 코루틴 하나만 실행)
- `FindObjectsOfType<FieldMonsterController>()` 사용 — `FindObjectsOfType<GameObject>()`는 너무 무겁다
- `activeIndicators` 딕셔너리(`GameObject → DirectionFieldIndicator`)로 개별 추적 — 코루틴 도중 몬스터가 제거될 수 있기 때문
- 코루틴 안에서 딕셔너리 직접 수정 시 예외 발생 → `toRemove` 리스트를 별도로 만들어 순회 후 일괄 삭제

**심볼 카운터 UI** (`Patches/GameFieldDefaultUIEnablePatch.cs`)
- `GameFieldDefaultUI.LoadFieldComplete` Postfix — 필드가 완전히 로드된 직후 실행
- 필드 진입 시 `Layout - FieldReward`에 심볼 몬스터 수 표시 (아이콘 + 텍스트)
- 아이콘 파일: `BepInEx\plugins\RayelleBX\Resources\symbol_monster.png`
- UI 계층 구조:
  ```
  Layout - FieldReward
    └─ Button - Item6          ← 이 패치가 생성
         ├─ Image - Icon        (symbol_monster.png를 런타임 로드한 Sprite)
         └─ Text - Count        (TextMeshProUGUI, ComponentHelper.CreateTMPro 사용)
  ```

**심볼 제거 업데이트** (`Patches/SymbolRemovePatch.cs`)
- `FieldMonsterController.RemoveMonster` Postfix
- 몬스터 제거 시 카운터 텍스트 업데이트 또는 UI 제거
- `GameFieldDefaultUIEnablePatch`와 역할 분담: 전자는 필드 진입 시 UI **생성**, 후자는 몬스터 제거마다 UI **갱신/삭제**

**퀵메뉴 대화 매크로** (`Patches/QuickMenuUIEnablePatch.cs`)
- `QuickMenuUI.SetMenu` Postfix — 퀵메뉴가 열릴 때마다 실행
- 퀵메뉴 하단 버튼 목록에 보라색(`UISprite.color`) 매크로 버튼 추가
- 버튼 클릭 시 `TalkMacroLoop` 코루틴 시작: 대화 버튼 클릭 → BalloonScriptUI Skip 버튼 대기 → 클릭 반복
- `_listenerBound` 플래그로 리스너 중복 등록 방지 — `SetMenu`는 퀵메뉴가 열릴 때마다 재호출되기 때문
- Q키 또는 오버레이 종료 버튼으로 중단 (`ComponentHelper.IsMacroRunning = false`)

**자동 먹이기 매크로** (`Patches/CharRecoveryUIEnablePatch.cs`)
- `CharRecoveryUI.SetUI` Postfix — 회복 탭이 열릴 때 실행
- 기존 Auto 버튼 옆에 "자동 먹이기" 버튼 추가 (`HorizontalLayoutGroup`으로 나란히 배치)
- `EatMacroLoop` 코루틴: 뒤로가기 → 연결 → 코스튬1 선택 → 코스튬 연결 → 코스튬0 선택 → 코스튬 연결 → 회복 2회 → 먹이기 N회 → 먹기 순서 자동화
- `GetFeedCount()`: `Text - TotalHealth` / `Text - Health` TMP 텍스트를 파싱해 `(total - current) / 2` 회 먹이기 아이템 클릭 횟수 산출
- Q키 또는 오버레이 종료 버튼으로 중단

**코스튬 ID 매핑 기록** (`Patches/CharUIEnablePatch.cs`)
- `CharUI.SetUI` Postfix — 캐릭터 UI가 열릴 때 실행
- 리플렉션으로 `CharCostumeUI`의 난독화 필드(`ὩὠὬὣὥὮὦὢὩὧὭ`)에서 코스튬 데이터 객체를 꺼내고, 난독화 프로퍼티(`ὪὫὫὢὩὦὤὪὧὫὡ`)로 CostumeID를 읽는다
- `{캐릭터명}_{코스튬명}` 형태로 `CostumeMapping.csv`에 누적 저장
- 리플렉션 실패 시 Exception을 catch하여 로그 출력 후 무시 (크래시 방지)

### 헬퍼 / 공통 인프라

**UIHelper** (`Helpers/UIHelper.cs`)
- `FindOrLog(path)` — `GameObject.Find` 실패 시 로그 출력
- `TryInvokeButton(path)` — 버튼을 찾아 `onClick.Invoke()`, 실패 시 false 반환
- `IsExistAndActive(path)` — 오브젝트 존재 여부 + `activeSelf` 확인 (매크로 루프 대기 조건)

**CoroutineHelper** (`Helpers/CoroutineHelper.cs`) + **CoroutineRunner** (`Components/CoroutineRunner.cs`)
- `GetOrCreateRunner()` — `MacroCoroutineRunner` GameObject에 `CoroutineRunner`를 붙여 반환, `DontDestroyOnLoad`로 씬 전환 시 유지
- `CoroutineRunner.Update()` — 매 프레임 Q키 감지 → `ComponentHelper.IsMacroRunning = false`로 매크로 중단
- 패치 클래스는 MonoBehaviour가 아니므로 코루틴을 직접 시작할 수 없어 Runner에 위임하는 패턴

**ComponentHelper 확장** (`Helpers/ComponentHelper.cs`)
- `IsMacroRunning` (static bool) — 매크로 실행 중 여부 플래그. CoroutineRunner·패치 클래스가 공유
- `CreateRectTransForm(go)` — anchorMin/Max를 0/1로 설정해 부모 전체를 채우는 RectTransform 추가
- `CreateOverlay(parent)` — 반투명 검정 오버레이 생성. "자동 클릭 중..." 텍스트 + 빨간 종료 버튼 포함. 종료 버튼은 QuickMenu·BalloonScript 오버레이를 비활성화하고 `IsMacroRunning = false`

**CostumeConfig** (`Config/CostumeConfig.cs`)
- CSV 경로: `BepInEx\plugins\RayelleBX\Resources\CostumeMapping.csv`
- `EnsureFile()` — 디렉터리·파일 없으면 생성 (헤더: `CostumeID,CostumeName`)
- `AppendMapping(id, name)` — 한 줄 추가
- `LoadAllCostumes()` — 저장된 CostumeID를 `HashSet<int>`로 반환
- `Split(new char[]{ ',' }, 2)` — .NET Framework 4.8에서는 `Split(char, int)` 오버로드 없음

### 리플렉션 패턴

```csharp
// DirectionFieldIndicator.Init() — 3번째 파라미터가 obfuscated enum 타입
MethodInfo initMethod = typeof(DirectionFieldIndicator).GetMethod("Init", ...);
ParameterInfo[] parameters = initMethod.GetParameters();
Type enumType = parameters[2].ParameterType;
object enumValue = Enum.ToObject(enumType, 1);
initMethod.Invoke(indicator, new object[] { playerTransform, monster, enumValue });
// (FieldObjectBase) 캐스팅 없이 monster를 직접 전달할 것
```

### 기타

- `System/Runtime/CompilerServices/RefSafetyRulesAttribute.cs` — C# 최신 언어 기능 사용 시 컴파일러가 내부적으로 삽입하려는 속성. .NET Framework 4.8 환경에는 기본 내장되지 않아 직접 선언해야 한다. `[Embedded]`나 `using Microsoft.CodeAnalysis` 없이 유지
- `AssemblyInfo.cs`의 `SkipVerification = true` — BepInEx 플러그인은 게임 DLL의 private/internal 멤버에 접근해야 하므로 .NET CLR의 IL 검증을 건너뛴다. 런타임 패치 환경에서는 일반적인 설정이다.
- `ComponentHelper.CreateTMPro()` — `TextMeshProUGUI` 생성 + `FontLocalizer` 설정. 게임은 언어 설정에 따라 폰트를 동적으로 교체하는 `FontLocalizer` 컴포넌트를 사용하므로, 플러그인에서 TMP를 직접 생성할 때도 `FontLocalizer`를 함께 붙여야 폰트가 깨지지 않는다. 리플렉션 실패 시 기본 폰트로 폴백하며 Warning 로그만 출력
- `CharUIEnablePatch`의 `__0` 파라미터 (난독화 타입 `ὬὡὤὡὯὦὫὫὫὥὭ`) — Postfix 시그니처에서 사용하지 않으면 생략 가능. Harmony는 선언된 파라미터만 주입하므로 타입을 모를 때는 파라미터 자체를 제거한다
- `string.Split(char, int)` — .NET Core/.NET 5+ 전용. Framework 4.8에서는 `Split(new char[]{ delimiter }, count)` 형태로 대체해야 한다
