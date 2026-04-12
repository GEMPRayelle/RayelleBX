# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

BrownDust 2 게임용 BepInEx 5 플러그인. HarmonyLib을 이용해 게임 메서드를 런타임에 패치하여 추가 기능을 제공한다.

- **게임 경로**: `C:\Neowiz\Browndust2\Browndust2_10000001`
- **BepInEx core**: `...\BepInEx\core\`
- **게임 Managed DLL**: `...\BrownDust II_Data\Managed\`
- **플러그인 배포 경로**: `...\BepInEx\plugins\RayelleBX\`
- **BepInEx 로그**: `...\BepInEx\LogOutput.log`
- **리소스 경로**: `...\BepInEx\plugins\RayelleBX\Resources\`

## 빌드 및 배포

```powershell
# PowerShell로 빌드 (빌드 완료 시 자동 배포)
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  RayelleBX.csproj /p:Configuration=Debug /nologo /v:minimal

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
- `__0`, `__1`, ... : 원본 메서드의 파라미터 (이름 난독화 시 순서 기반 참조, `object`로 받아 캐스팅 가능)

### 패치 등록 방식

`Plugin.cs`의 `Awake()`에서 두 가지 방식으로 패치를 등록한다:

1. **`TryPatchAll` 래퍼 방식** — `GameFieldDefaultUIEnablePatch`, `SymbolRemovePatch`, `QuickMenuUIEnablePatch`, `CharRecoveryUIEnablePatch`는 `[HarmonyPatch]` 속성이 있으므로 `TryPatchAll(_harmony, typeof(...))` 사용
2. **`ApplyPatches()` 수동 방식** — `OverwhelmIndicatorPatch`, `CharUIEnablePatch`, `GachaMacroUIEnablePatch`는 `ApplyPatches(harmony)` 메서드를 직접 호출. 내부에서 `AccessTools.Method` + try-catch로 각 패치를 개별 보호

> **중요**: `OverwhelmIndicatorPatch`에 `TargetMethods()` (HarmonyX 자동 감지 이름)를 쓰면 `PatchAll` 시 이중 호출된다. 반드시 `GetTargetMethods()`로 이름을 유지하고 `Plugin.cs`에서 수동 패치할 것.

> **중요**: PatchAll 실패는 이후 패치 등록을 중단시킨다. 모든 PatchAll 호출은 반드시 `TryPatchAll`로 감싸야 한다.

### Obfuscated 메서드 처리

`Assembly-CSharp.dll`은 난독화되어 있어 메서드/필드명이 게임 업데이트마다 변경된다.

- **TalentSkillManager 패치 후보**: 게임 업데이트 시 이름이 바뀌므로 `OverwhelmIndicatorPatch.GetTargetMethods()`에 후보 이름을 배열로 나열 — 존재하는 것만 패치, 없는 것은 Warning 로그
- **FontLocalizer 필드** (v2026-04-11 기준): `ὡὥὢὬὡὭὯὭὥὦὢ` (fontName), `ὮὯὡὨὬὯὭὬὯὫὫ` (fontMaterial), apply메서드: `ὤὮὫὯὦὭὥὦὫὩὢ` — 탐색 방법은 `Docs/FontLocalizerReverseEngineering.md` 참조
- **CharCostumeUI 필드** (v2026-04-11 기준): 코스튬 데이터 필드 `ὩὠὬὣὥὮὦὢὩὧὭ`, CostumeID 프로퍼티 `ὪὫὫὢὩὦὤὪὧὫὡ`
- obfuscated 이름 탐색은 Mono.Cecil로 `Assembly-CSharp.dll` 분석 — `Docs/Maintenance.md` 참조

### 주요 기능

**OverwhelmIndicator** (`Patches/OverwhelmIndicatorPatch.cs`)
- `TalentSkillManager`의 압도 스킬 메서드 Postfix로 트리거
- 필드 내 `Symbol_` 이름의 `FieldMonsterController` 오브젝트를 탐색
- `DirectionFieldMark0`을 복제해 빨간 방향 인디케이터 생성 (`Init()` 리플렉션 호출)
- `_isRunning` 플래그로 중복 실행 방지
- `activeIndicators` 딕셔너리(`GameObject → DirectionFieldIndicator`)로 개별 추적
- 코루틴 안에서 딕셔너리 직접 수정 시 예외 발생 → `toRemove` 리스트로 분리해 일괄 삭제

**심볼 카운터 UI** (`Patches/GameFieldDefaultUIEnablePatch.cs`)
- `GameFieldDefaultUI.LoadFieldComplete` Postfix — 필드가 완전히 로드된 직후 실행
- 필드 진입 시 `Layout - FieldReward`에 심볼 몬스터 수 표시 (아이콘 + 텍스트)
- 아이콘 파일: `Resources/symbol_monster.png`
- UI 계층 구조: `Layout - FieldReward → Button - Item6 → Image - Icon / Text - Count`

**심볼 제거 업데이트** (`Patches/SymbolRemovePatch.cs`)
- `FieldMonsterController.RemoveMonster` Postfix
- 몬스터 제거 시 카운터 텍스트 업데이트 또는 UI 제거
- `GameFieldDefaultUIEnablePatch`와 역할 분담: 전자는 진입 시 **생성**, 후자는 제거마다 **갱신/삭제**

**퀵메뉴 대화 매크로** (`Patches/QuickMenuUIEnablePatch.cs`)
- `QuickMenuUI.SetMenu` Postfix — 퀵메뉴가 열릴 때마다 실행
- 퀵메뉴 하단 버튼 목록에 보라색 매크로 버튼 추가
- `TalkMacroLoop` 코루틴: 대화 버튼 클릭 → BalloonScriptUI Skip 버튼 대기 → 클릭 반복
- `_listenerBound` 플래그로 리스너 중복 등록 방지

**자동 먹이기 매크로** (`Patches/CharRecoveryUIEnablePatch.cs`)
- `CharRecoveryUI.SetUI` Postfix — 회복 탭이 열릴 때 실행
- 기존 Auto 버튼 옆에 "자동 먹이기" 버튼 추가
- `EatMacroLoop` 코루틴: 뒤로가기 → 연결 → 코스튬 선택/연결 × 2 → 회복 → 먹이기 N회 순서 자동화
- `GetFeedCount()`: `Text - TotalHealth` / `Text - Health` TMP 파싱 → `(total - current) / 2`

**코스튬 ID 매핑 기록** (`Patches/CharUIEnablePatch.cs`)
- `CharUI.ShowUI()` (파라미터 없는 오버로드) 수동 패치 — `ApplyPatches()` 방식
- 리플렉션으로 `CharCostumeUI`의 난독화 필드에서 CostumeID 추출
- `{캐릭터명}_{코스튬명}` 형태로 `CostumeMapping.csv`에 누적 저장
- 이 CSV는 GachaMacro의 UR 판정에 사용됨

**무한뽑기 매크로** (`Patches/GachaMacroUIEnablePatch.cs`)
- 패치 ①: `GachaResultUI.SetActive(bool)` Postfix — 결과 UI 활성화 시 `Button - Redraw`를 **복제**해 "자동 뽑기" 버튼 생성
- 패치 ②: `GachaResultUI.SetResult(...)` Postfix — `__0`(List, obfuscated) 파라미터에서 BackingField로 CostumeID 추출, UR·위시 코스튬 수 집계
- 버튼 위치: `UIRoot/Mask/Layout - InfiniteGachaButton/` 하위, `Button - Redraw` 옆
- `AutoGachaLoop` 코루틴 흐름:
  1. `Button - Redraw` 활성화 대기 (full path `GameObject.Find`)
  2. 클릭 → `GachaInfinitePopupUI` YES 버튼 대기/클릭
  3. `Button - Skip` 반복 클릭 (애니메이션 스킵)
  4. `SetResult` 호출 대기 (`ResultReceived` 플래그)
  5. `LastConditionMet` 확인 → 충족 시 종료, 미충족 시 반복
- UR 판정: `CostumeConfig.LoadAllCostumes()`(CostumeMapping.csv)에 포함된 CostumeID
- 위시 판정: `PluginConfig.GachaWishCostumes`(setting.cfg `WishCostume` 목록)

### 헬퍼 / 공통 인프라

**UIHelper** (`Helpers/UIHelper.cs`)
- `FindOrLog(path)` — `GameObject.Find` 실패 시 로그 출력
- `TryInvokeButton(path)` / `TryInvokeButton(go, label)` — 버튼을 찾아 `onClick.Invoke()`
- `IsExistAndActive(path)` — 오브젝트 존재 여부 + `activeSelf` 확인

**CoroutineHelper** (`Helpers/CoroutineHelper.cs`) + **CoroutineRunner** (`Components/CoroutineRunner.cs`)
- `GetOrCreateRunner()` — `MacroCoroutineRunner` GameObject에 `CoroutineRunner`를 붙여 반환, `DontDestroyOnLoad`로 씬 전환 시 유지
- `CoroutineRunner.Update()` — 매 프레임 Q키 감지 → `ComponentHelper.IsMacroRunning = false`로 매크로 중단
- 패치 클래스는 MonoBehaviour가 아니므로 코루틴을 직접 시작할 수 없어 Runner에 위임하는 패턴

**ComponentHelper** (`Helpers/ComponentHelper.cs`)
- `IsMacroRunning` (static bool) — 매크로 실행 중 여부 플래그. CoroutineRunner·패치 클래스 공유
- `CreateRectTransForm(go)` — anchorMin/Max를 0/1로 설정해 부모 전체를 채우는 RectTransform 추가
- `CreateOverlay(parent)` — 반투명 검정 오버레이 생성. "자동 클릭 중..." 텍스트 + 빨간 종료 버튼
- `CreateTMPro(go, text, color, size)` — `TextMeshProUGUI` 생성 + `FontLocalizer` 설정

**CostumeConfig** (`Config/CostumeConfig.cs`)
- CSV 경로: `Resources/CostumeMapping.csv`
- `EnsureFile()` — 파일 없으면 생성 (헤더: `CostumeID,CostumeName`)
- `AppendMapping(id, name)` — 한 줄 추가
- `LoadAllCostumes()` — 저장된 CostumeID를 `HashSet<int>`로 반환 (GachaMacro UR 판정에 사용)

**PluginConfig** (`Helpers/PluginConfig.cs`)
- `setting.cfg` 파싱. `WishCostume` 키 이후 `숫자,이름` 패턴 줄을 `GachaWishCostumes`에 추가
- `GachaStepDelay`, `GachaWaitResultTimeout`, `GachaMinUR`, `GachaEssentialUR` 포함

### 리플렉션 패턴

```csharp
// 1. obfuscated enum 파라미터가 있는 메서드 호출
MethodInfo initMethod = typeof(DirectionFieldIndicator).GetMethod("Init", ...);
Type enumType = initMethod.GetParameters()[2].ParameterType;
object enumValue = Enum.ToObject(enumType, 1);
initMethod.Invoke(indicator, new object[] { playerTransform, monster, enumValue });
// (FieldObjectBase) 캐스팅 없이 monster를 직접 전달할 것

// 2. BackingField에서 int 값 추출 (GachaMacro SetResult)
FieldInfo backingField = item.GetType()
    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
    .FirstOrDefault(f => f.Name.Contains("BackingField"));
object raw = backingField.GetValue(item);
int id = raw is int i ? i : int.Parse(raw.ToString());

// 3. Harmony __0 파라미터를 IEnumerable로 순회 (obfuscated List<T>)
private static void Postfix(GachaResultUI __instance, object __0)
{
    IEnumerable list = __0 as IEnumerable;
    foreach (object item in list) { ... }
}
```

### 기타 주의사항

- `System/Runtime/CompilerServices/RefSafetyRulesAttribute.cs` — .NET Framework 4.8 환경에서 C# 최신 기능 사용 시 컴파일러가 요구. `[Embedded]` 없이 유지
- `AssemblyInfo.cs`의 `SkipVerification = true` — 게임 DLL의 private/internal 멤버 접근을 위해 IL 검증 스킵. BepInEx 플러그인 표준 설정
- `string.Split(char, int)` — .NET Framework 4.8에서는 `Split(new char[]{ delimiter }, count)` 형태로 대체
- `ComponentHelper.CreateTMPro()` — TMP 직접 생성 시 `FontLocalizer`를 함께 붙여야 폰트가 깨지지 않음. 리플렉션 실패 시 기본 폰트로 폴백하며 Warning 로그만 출력
- 버튼을 게임 기존 버튼에서 **복제**(Instantiate)할 경우 `TutorialFocusTarget` 컴포넌트를 Destroy해야 튜토리얼 시스템과 충돌 방지
- `GameObject.Find(fullPath)` 는 씬 전체 탐색이므로 매 프레임 호출 금지. 대기 루프에서는 `WaitForSeconds(0.2f)` 간격으로 폴링
