# FontLocalizer Obfuscated 필드/메서드 탐색 가이드

게임 업데이트 시 `Assembly-CSharp.dll`의 난독화된 이름이 바뀔 수 있다.
이 문서는 Mono.Cecil을 이용해 `FontLocalizer`의 필드명과 apply 메서드를 찾는 과정을 기록한다.

---

## 배경

`ComponentHelper.CreateFontLocalizer()`는 생성한 `TextMeshProUGUI`에 게임 전용 폰트(SCDreamExtraBold)를 적용하기 위해 `FontLocalizer` 컴포넌트를 사용한다. 이를 위해 세 가지 obfuscated 이름이 필요하다:

| 역할 | 타입 | 현재 이름 (v2026-04-23) |
|------|------|------------------------|
| fontName 필드 | `string` | `ὩὠὮὮὢὮὭὤὡὥὧ` |
| fontMaterial 필드 | `string` | `ὦὫὭὥὢὠὮὭὩὦὯ` |
| apply 메서드 | `void()` | `ὢὫὨὪὮὤὧὭὤὥὡ` |

게임 업데이트 후 Warning 로그가 뜨면 아래 절차로 새 이름을 찾는다.

---

## 탐색 절차

### 1단계 — 분석 툴 프로젝트 준비

```
mkdir C:\Temp\FontLocalizerTool
```

`FontLocalizerTool.csproj`:
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

### 2단계 — FontLocalizer 클래스의 필드 목록 확인

`_textTarget`(TMP_Text)은 이름이 유지되고, `string` 타입 필드 두 개가 fontName과 fontMaterial이다.

```csharp
var asm = AssemblyDefinition.ReadAssembly(
    @"C:\Neowiz\Browndust2\Browndust2_10000001\BrownDust II_Data\Managed\Assembly-CSharp.dll");

var type = asm.MainModule.Types.First(t => t.Name == "FontLocalizer");

foreach (var f in type.Fields)
    Console.WriteLine($"[{f.FieldType.Name}] {f.Name}");
```

**기대 결과**: `String` 타입 필드가 2개 나온다 → 이 두 개가 fontName, fontMaterial.

### 3단계 — Awake()가 호출하는 메서드 확인

apply 메서드는 `Awake()`에서 직접 호출된다.

```csharp
var awake = type.Methods.First(m => m.Name == "Awake");
foreach (var instr in awake.Body.Instructions)
{
    if ((instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt)
        && instr.Operand is MethodReference mr)
        Console.WriteLine($"{mr.DeclaringType.Name}::{mr.Name}");
}
```

`FontLocalizer::` 로 시작하는 메서드 중 파라미터가 없는 것들이 후보다.

### 4단계 — 후보 메서드의 IL 덤프로 apply 메서드 특정

Awake()가 호출하는 FontLocalizer 메서드들의 IL을 출력해 역할을 판별한다.

```csharp
var m = type.Methods.First(x => x.Name == "후보이름");
foreach (var instr in m.Body.Instructions)
{
    string operand = "";
    if (instr.Operand is FieldReference fr)
        operand = fr.DeclaringType.Name + "::" + fr.Name + " (" + fr.FieldType.Name + ")";
    else if (instr.Operand is MethodReference mr)
        operand = mr.DeclaringType.Name + "::" + mr.Name;
    Console.WriteLine($"{instr.OpCode,-12} {operand}");
}
```

**판별 기준**:
- `_textTarget`, fontName 필드, fontMaterial 필드를 읽은 뒤 외부 유틸 메서드를 호출하면 → **apply 메서드**
- fontName/fontMaterial 필드가 비어있을 때 현재 폰트명을 읽어 채워넣으면 → 초기화 메서드 (apply 아님)

apply 메서드의 IL 패턴:
```
ldfld   FontLocalizer::_textTarget
ldfld   FontLocalizer::[fontName 필드]
ldfld   FontLocalizer::[fontMaterial 필드]
call    [외부유틸클래스]::[폰트적용메서드]
ret
```

### 5단계 — ComponentHelper.cs 업데이트

찾은 이름 세 개를 `ComponentHelper.CreateFontLocalizer()`에 반영한다:

```csharp
FieldInfo fFontName    = typeof(FontLocalizer).GetField("[새 fontName 필드명]",    BindingFlags.Instance | BindingFlags.NonPublic);
FieldInfo fFontMaterial = typeof(FontLocalizer).GetField("[새 fontMaterial 필드명]", BindingFlags.Instance | BindingFlags.NonPublic);
MethodInfo mApply      = typeof(FontLocalizer).GetMethod("[새 apply 메서드명]",     BindingFlags.Instance | BindingFlags.NonPublic);
```

빌드 후 게임 실행 시 Warning 로그가 사라지면 정상 적용된 것이다.

---

## 업데이트 이력

| 날짜 | fontName | fontMaterial | apply 메서드 |
|------|----------|--------------|-------------|
| 2026-04-23 | `ὩὠὮὮὢὮὭὤὡὥὧ` | `ὦὫὭὥὢὠὮὭὩὦὯ` | `ὢὫὨὪὮὤὧὭὤὥὡ` |
| 2026-04-11 | `ὡὥὢὬὡὭὯὭὥὦὢ` | `ὮὯὡὨὬὯὭὬὯὫὫ` | `ὤὮὫὯὦὭὥὦὫὩὢ` |
| (이전) | `ὠὣὪὥὩὯὩὠὤὢὫ` | `ὧὬὤὧὥὪὭὮὡὪὨ` | `ὢὡὨὧὧὤὥὤὢὢὣ` |
