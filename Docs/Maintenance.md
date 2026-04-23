# RayelleBX 유지보수 가이드

게임 업데이트 이후 플러그인이 동작하지 않을 때 원인 파악 및 수정 절차.

---

## 목차

1. [진단 체크리스트](#진단-체크리스트)
2. [난독화 이름 변경 대응](#난독화-이름-변경-대응)
   - [Mono.Cecil 분석 환경 준비](#monocecil-분석-환경-준비)
   - [FontLocalizer 필드 탐색](#fontlocalizer-필드-탐색)
   - [CharCostumeUI 필드 탐색](#charcostumeui-필드-탐색)
   - [TalentSkillManager 메서드 탐색](#talentskillmanager-메서드-탐색)
   - [GachaResultUI SetResult 파라미터 타입 탐색](#gacharesultui-setresult-파라미터-타입-탐색)
3. [UI 경로 변경 대응](#ui-경로-변경-대응)
4. [패치 대상 메서드 변경 대응](#패치-대상-메서드-변경-대응)
5. [알려진 에러 패턴 및 해결책](#알려진-에러-패턴-및-해결책)
6. [난독화 이름 변경 이력](#난독화-이름-변경-이력)

---

## 진단 체크리스트

게임 업데이트 후 플러그인 동작이 이상할 때 가장 먼저 로그를 확인한다.

**로그 위치**: `C:\Neowiz\Browndust2\Browndust2_10000001\BepInEx\LogOutput.log`

### 로그로 확인할 항목

```
[Info ] Plugin rayelle.bx is loaded!              ← 플러그인 로드 성공
[Info ] [GachaMacro] 패치 등록: GachaResultUI.SetActive   ← 각 패치 등록 성공
[Info ] Harmony Patch Complete                     ← 모든 패치 등록 완료
```

**문제 징후별 원인**:

| 로그 패턴 | 원인 | 해결 |
|-----------|------|------|
| `PatchAll 실패: ... 메서드를 찾을 수 없음` | 패치 대상 메서드명 변경 | [패치 대상 메서드 변경 대응](#패치-대상-메서드-변경-대응) |
| `[CharUI] ShowUI() 메서드를 찾을 수 없음` | `CharUI.ShowUI` 메서드명 변경 | `CharUIEnablePatch.ApplyPatches()` 메서드명 수정 |
| `[GachaMacro] 메서드 없음: GachaResultUI.SetActive` | `GachaResultUI.SetActive` 없음 | [패치 대상 메서드 변경 대응](#패치-대상-메서드-변경-대응) |
| `[FontLocalizer]` Warning 로그 | FontLocalizer 필드명 변경 | [FontLocalizer 필드 탐색](#fontlocalizer-필드-탐색) |
| `Harmony Patch Complete` 없음 | Awake() 크래시 | 이전 패치에서 예외 발생, 개별 확인 |
| 버튼이 생성되나 UR 판정이 0 | BackingField 구조 변경 또는 CostumeMapping.csv 비어있음 | [GachaResultUI SetResult 파라미터 탐색](#gacharesultui-setresult-파라미터-타입-탐색) |

---

## 난독화 이름 변경 대응

### Mono.Cecil 분석 환경 준비

게임 DLL은 `Mono.Cecil`로 정적 분석한다. BepInEx에 이미 포함되어 있어 별도 설치 불필요.

**분석용 임시 프로젝트 생성**:

```
mkdir C:\Temp\CecilTool
```

`C:\Temp\CecilTool\CecilTool.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Mono.Cecil">
      <HintPath>C:\Neowiz\Browndust2\Browndust2_10000001\BepInEx\core\Mono.Cecil.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

`Program.cs` 상단에 항상 추가:
```csharp
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;

var asm = AssemblyDefinition.ReadAssembly(
    @"C:\Neowiz\Browndust2\Browndust2_10000001\BrownDust II_Data\Managed\Assembly-CSharp.dll");
```

---

### FontLocalizer 필드 탐색

**증상**: 텍스트 폰트가 깨지거나 `[FontLocalizer] Warning` 로그 출력

**목표**: `fontName`, `fontMaterial` 필드명, `apply` 메서드명 갱신

**탐색 코드**:

```csharp
// 1단계 — FontLocalizer의 string 필드 목록 출력
var type = asm.MainModule.Types.First(t => t.Name == "FontLocalizer");

Console.WriteLine("=== Fields ===");
foreach (var f in type.Fields)
    Console.WriteLine($"  [{f.FieldType.Name}] {f.Name}");

// 기대 결과: string 타입 필드 2개 → fontName, fontMaterial

// 2단계 — Awake()가 호출하는 FontLocalizer 내부 메서드 확인
var awake = type.Methods.First(m => m.Name == "Awake");
Console.WriteLine("\n=== Awake() calls ===");
foreach (var instr in awake.Body.Instructions)
{
    if ((instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt)
        && instr.Operand is MethodReference mr
        && mr.DeclaringType.Name == "FontLocalizer")
        Console.WriteLine($"  {mr.Name}");
}

// 3단계 — 후보 메서드 IL 덤프로 apply 메서드 특정
var candidate = type.Methods.First(m => m.Name == "후보이름");
Console.WriteLine($"\n=== {candidate.Name} IL ===");
foreach (var instr in candidate.Body.Instructions)
{
    string operand = instr.Operand switch {
        FieldReference fr => $"{fr.Name} ({fr.FieldType.Name})",
        MethodReference mr => $"{mr.DeclaringType.Name}::{mr.Name}",
        _ => instr.Operand?.ToString() ?? ""
    };
    Console.WriteLine($"  {instr.OpCode,-12} {operand}");
}
// apply 메서드 판별: _textTarget + fontName필드 + fontMaterial필드 → 외부 유틸 호출
```

**업데이트 위치**: `Helpers/ComponentHelper.cs` — `CreateFontLocalizer()` 내부 3개 상수

```csharp
FieldInfo fFontName     = typeof(FontLocalizer).GetField("[새 fontName]",     BindingFlags.Instance | BindingFlags.NonPublic);
FieldInfo fFontMaterial = typeof(FontLocalizer).GetField("[새 fontMaterial]", BindingFlags.Instance | BindingFlags.NonPublic);
MethodInfo mApply       = typeof(FontLocalizer).GetMethod("[새 apply 메서드]", BindingFlags.Instance | BindingFlags.NonPublic);
```

---

### CharCostumeUI 필드 탐색

**증상**: `CostumeMapping.csv`에 기록이 안 됨, `[Costume Patch] Exception` 로그

**목표**: 코스튬 데이터 필드명, CostumeID 프로퍼티명 갱신

**탐색 코드**:

```csharp
var type = asm.MainModule.Types.First(t => t.Name == "CharCostumeUI");

// CharCostumeUI의 모든 필드 출력 — 타입명으로 코스튬 데이터 객체 필드 식별
Console.WriteLine("=== CharCostumeUI Fields ===");
foreach (var f in type.Fields)
    Console.WriteLine($"  [{f.FieldType.Name}] {f.Name}");

// 코스튬 데이터 타입의 프로퍼티 목록 — int 타입 중 100~99999 범위가 CostumeID
var dataTypeName = "찾은_코스튬데이터_타입명";
var dataType = asm.MainModule.Types.First(t => t.Name == dataTypeName);
Console.WriteLine($"\n=== {dataTypeName} Properties ===");
foreach (var p in dataType.Properties)
    Console.WriteLine($"  [{p.PropertyType.Name}] {p.Name}");
```

**업데이트 위치**: `Patches/CharUIEnablePatch.cs` — `Postfix()` 내 2개 리플렉션 문자열

```csharp
object costumeData = typeof(CharCostumeUI)
    .GetField("[새 필드명]", BindingFlags.Instance | BindingFlags.NonPublic)
    ?.GetValue(costumeUI);

object costumeId = costumeData.GetType()
    .GetProperty("[새 프로퍼티명]", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
    ?.GetValue(costumeData);
```

---

### TalentSkillManager 메서드 탐색

**증상**: 전투 필드에서 압도 인디케이터가 표시되지 않음

**목표**: 압도 스킬 메서드 후보 이름 갱신

**탐색 코드**:

```csharp
var type = asm.MainModule.Types.First(t => t.Name == "TalentSkillManager");
// 압도 스킬 메서드는 특정 state machine 타입을 반환
// 이전에 알려진 state machine 타입명: ὪὤὮὥὬὮὠὦὦὫὧ

Console.WriteLine("=== TalentSkillManager Methods (IEnumerator 반환) ===");
foreach (var m in type.Methods)
{
    // IEnumerator 반환 메서드만 필터링 (코루틴)
    if (m.ReturnType.Name.Contains("IEnumerator") ||
        m.ReturnType.Name.Contains("Enumerator"))
        Console.WriteLine($"  {m.Name}  (params: {m.Parameters.Count})");
}
```

**업데이트 위치**: `Patches/OverwhelmIndicatorPatch.cs` — `GetTargetMethods()` 배열에 새 이름 추가

```csharp
private static readonly string[] MethodCandidates = new[]
{
    "[이전 이름]",
    "[새로 발견된 이름]",
};
```

---

### GachaResultUI SetResult 파라미터 타입 탐색

**증상**: 뽑기 결과에서 UR이 항상 0으로 집계됨

**목표**: `SetResult`의 첫 번째 파라미터(`List<ObfuscatedType>`) 내부 구조 확인

**탐색 코드**:

```csharp
var type = asm.MainModule.Types.First(t => t.Name == "GachaResultUI");
var setResult = type.Methods.First(m => m.Name == "SetResult");

Console.WriteLine("=== SetResult Parameters ===");
foreach (var p in setResult.Parameters)
    Console.WriteLine($"  [{p.ParameterType.Name}] {p.Name}");

// 첫 번째 파라미터가 List<T> 이면 T의 이름을 확인
// T 타입의 필드/프로퍼티 목록 출력
var itemTypeName = "확인된_아이템_타입명";
var itemType = asm.MainModule.Types.FirstOrDefault(t => t.Name == itemTypeName);
if (itemType != null)
{
    Console.WriteLine($"\n=== {itemTypeName} Fields ===");
    foreach (var f in itemType.Fields)
        Console.WriteLine($"  [{f.FieldType.Name}] {f.Name}");

    Console.WriteLine($"\n=== {itemTypeName} Properties ===");
    foreach (var p in itemType.Properties)
        Console.WriteLine($"  [{p.PropertyType.Name}] {p.Name}");
}
```

**현재 구현**: BackingField 이름에 `"BackingField"` 포함 여부로 탐색.  
구조가 바뀌어 BackingField 패턴이 없으면 int 타입 필드 중 100~99999 범위 값으로 탐색하도록 수정.

**업데이트 위치**: `Patches/GachaMacroUIEnablePatch.cs` — `SetResult_Postfix()` 내 백킹 필드 탐색 로직

---

## UI 경로 변경 대응

게임 업데이트 시 UI 계층 구조가 바뀌면 `GameObject.Find(fullPath)` 탐색이 실패한다.

### 확인 방법

BepInEx 로그에서 `Not Found` 메시지를 찾는다:

```
[Info ] [UIHelper] 'Button - Redraw' Not Found
```

또는 패치는 성공했으나 버튼 클릭 후 아무 동작이 없을 때.

### 경로 재탐색 방법

Unity Explorer (BepInEx 플러그인) 또는 아래 런타임 로그 코드로 확인:

```csharp
// 임시 디버그 Postfix에 추가해 계층 구조 출력
private static void DumpHierarchy(Transform t, int depth = 0)
{
    Plugin.Log.LogInfo($"{new string(' ', depth * 2)}{t.name}");
    foreach (Transform child in t)
        DumpHierarchy(child, depth + 1);
}

// SetActive Postfix에서 임시 호출
DumpHierarchy(__instance.transform);
```

### 경로가 바뀐 경우 업데이트할 위치

**무한뽑기 매크로** (`GachaMacroUIEnablePatch.cs`):

```csharp
private const string RedrawBtnRelPath  = "UIRoot/Mask/Layout - InfiniteGachaButton/Button - Redraw";
private const string AutoBtnRelPath    = "UIRoot/Mask/Layout - InfiniteGachaButton/Button - AutoGacha";
private const string RedrawBtnFullPath = "Singleton (DontDestroy)/AppManager/UI/GachaResultUI(Clone)/UIRoot/Mask/Layout - InfiniteGachaButton/Button - Redraw";
private const string YesBtnFullPath    = "Singleton (DontDestroy)/AppManager/UI/GachaInfinitePopupUI(Clone)/Button - background/Parent/Image - Backgrond/ButtonYesNo/Button - YES";
private const string SkipBtnFullPath   = "Singleton (DontDestroy)/AppManager/UI/GachaResultUI(Clone)/UIRoot/Mask/Button - Skip";
```

---

## 패치 대상 메서드 변경 대응

게임 업데이트로 메서드명이 바뀌거나 삭제된 경우.

### 신규 메서드 탐색

```csharp
// 클래스의 모든 public 메서드 목록
var type = asm.MainModule.Types.First(t => t.Name == "GachaResultUI");
foreach (var m in type.Methods.Where(m => m.IsPublic))
    Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name))})");
```

### 업데이트 위치별 메서드명

| 기능 | 클래스 | 현재 메서드 | 위치 |
|------|--------|------------|------|
| 코스튬 기록 | `CharUI` | `ShowUI()` (파라미터 없음) | `CharUIEnablePatch.ApplyPatches()` |
| 심볼 카운터 | `GameFieldDefaultUI` | `LoadFieldComplete` | `[HarmonyPatch]` 속성 |
| 심볼 갱신 | `FieldMonsterController` | `RemoveMonster` | `[HarmonyPatch]` 속성 |
| 퀵메뉴 매크로 | `QuickMenuUI` | `SetMenu` | `[HarmonyPatch]` 속성 |
| 먹이기 매크로 | `CharRecoveryUI` | `SetUI` | `[HarmonyPatch]` 속성 |
| 가챠 버튼 생성 | `GachaResultUI` | `SetActive` | `GachaMacroUIEnablePatch.ApplyPatches()` |
| 가챠 결과 판정 | `GachaResultUI` | `SetResult` | `GachaMacroUIEnablePatch.ApplyPatches()` |

---

## 알려진 에러 패턴 및 해결책

### PatchAll이 Awake() 전체를 중단시킨다

**원인**: `[HarmonyPatch]` 속성에 지정된 메서드가 없을 때 `PatchAll`이 예외를 던지면 `Awake()`가 중단되어 이후 패치가 모두 등록되지 않는다.

**해결**: 모든 `PatchAll` 호출을 `TryPatchAll`로 감싼다 (`Plugin.cs` 참고).  
`[HarmonyPatch]` 속성 없이 `ApplyPatches()` 수동 등록 방식으로 전환하면 더 안전하다.

```csharp
// 안전한 개별 패치 등록 패턴
private static void TryPatch(Harmony harmony, Type targetType, string methodName,
    string postfixName, string label)
{
    try {
        MethodInfo target = AccessTools.Method(targetType, methodName);
        if (target == null) { Plugin.Log.LogWarning($"메서드 없음: {label}"); return; }
        harmony.Patch(target, postfix: new HarmonyMethod(typeof(MyPatch), postfixName));
    } catch (Exception e) {
        Plugin.Log.LogError($"패치 실패: {label} — {e.Message}");
    }
}
```

### 버튼이 생성되나 클릭 리스너가 중복 등록된다

**원인**: `SetActive` / `SetMenu` 등 매번 호출되는 메서드에서 버튼 존재 여부 없이 리스너를 추가한다.

**해결**: 버튼 생성 전 `transform.Find("Button - AutoGacha") != null` 체크.  
리스너 추가 전 항상 `btn.onClick.RemoveAllListeners()` 호출.

### Harmony `object __0`으로 파라미터를 받으면 null이다

**원인**: Harmony는 선언된 파라미터 타입이 실제 타입과 일치하지 않으면 주입을 건너뛴다.  
`object`는 일반적으로 동작하지만, 값 타입(struct, int 등)은 박싱이 필요해 null로 올 수 있다.

**해결**: `IEnumerable` 또는 정확한 base type으로 선언. 값 타입이면 파라미터 직접 사용 대신 `__instance`를 통해 접근.

### `TutorialFocusTarget` 컴포넌트가 복제된 버튼에 남아 있다

**원인**: 기존 버튼을 `Instantiate`로 복제하면 부착된 컴포넌트가 모두 복사된다.

**해결**: 복제 직후 `TutorialFocusTarget` 컴포넌트를 `Destroy`. (`GachaMacroUIEnablePatch.SetActive_Postfix` 참고)

---

## 난독화 이름 변경 이력

### FontLocalizer

| 날짜 | fontName 필드 | fontMaterial 필드 | apply 메서드 |
|------|--------------|-------------------|-------------|
| 2026-04-23 | `ὩὠὮὮὢὮὭὤὡὥὧ` | `ὦὫὭὥὢὠὮὭὩὦὯ` | `ὢὫὨὪὮὤὧὭὤὥὡ` |
| 2026-04-11 | `ὡὥὢὬὡὭὯὭὥὦὢ` | `ὮὯὡὨὬὯὭὬὯὫὫ` | `ὤὮὫὯὦὭὥὦὫὩὢ` |
| (이전) | `ὠὣὪὥὩὯὩὠὤὢὫ` | `ὧὬὤὧὥὪὭὮὡὪὨ` | `ὢὡὨὧὧὤὥὤὢὢὣ` |

### CharCostumeUI

| 날짜 | 코스튬 데이터 필드 | CostumeID 프로퍼티 |
|------|--------------------|-------------------|
| 2026-04-23 | `ὥὪὩὢὣὯὩὨὮὫὢ` (타입: `CostumeDBInfo`, protobuf) | `Id` (비난독화) |
| 2026-04-11 | `ὩὠὬὣὥὮὦὢὩὧὭ` | `ὪὫὫὢὩὦὤὪὧὫὡ` |
