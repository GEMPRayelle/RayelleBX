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

### 패치 등록 방식

`Plugin.cs`의 `Awake()`에서 두 가지 방식으로 패치를 등록한다:

1. **`[HarmonyPatch]` 속성 방식** — `GameFieldDefaultUIEnablePatch`, `SymbolRemovePatch`는 클래스에 속성이 있으므로 `_harmony.PatchAll(typeof(...))` 사용
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
- `_isRunning` 플래그로 중복 실행 방지
- `FindObjectsOfType<FieldMonsterController>()` 사용 — `FindObjectsOfType<GameObject>()`는 너무 무겁다

**심볼 카운터 UI** (`Patches/GameFieldDefaultUIEnablePatch.cs`)
- `GameFieldDefaultUI.LoadFieldComplete` Postfix
- 필드 진입 시 `Layout - FieldReward`에 심볼 몬스터 수 표시 (아이콘 + 텍스트)
- 아이콘 파일: `BepInEx\plugins\RayelleBX\Resources\symbol_monster.png`

**심볼 제거 업데이트** (`Patches/SymbolRemovePatch.cs`)
- `FieldMonsterController.RemoveMonster` Postfix
- 몬스터 제거 시 카운터 텍스트 업데이트 또는 UI 제거

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

- `System/Runtime/CompilerServices/RefSafetyRulesAttribute.cs` — C# 최신 언어 기능 사용 시 필요한 컴파일러 내부 속성. `[Embedded]`나 `using Microsoft.CodeAnalysis` 없이 유지
- `ComponentHelper.CreateTMPro()` — `TextMeshProUGUI` 생성 + `FontLocalizer` 설정. 리플렉션 실패 시 기본 폰트로 폴백하며 Warning 로그만 출력
