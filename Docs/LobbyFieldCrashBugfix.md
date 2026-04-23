# 로비→필드 UI 겹침 크래시 수정 기록

로비에서 필드로 복귀할 때 UI가 겹치며 게임이 강제 종료되는 버그의 원인 분석 및 수정 내용.

---

## 증상

- 플러그인이 **없는** 순정 상태에서는 재현되지 않음
- 필드에서 로비 버튼을 클릭한 뒤 다시 필드로 복귀할 때 **100% 재현**
- 복귀 순간 필드 UI와 로비 UI가 동시에 표시(겹침)되며 강제 종료
- 매크로 실행 여부와 무관 (일반 게임 플레이 중에도 발생)
- 씬 전환이 아닌 동일 씬 내 UI `SetActive` 전환 방식

---

## 수정 전 코드 (원인 코드)

`GameFieldDefaultUIEnablePatch.Postfix` — `LoadFieldComplete` 호출마다 실행:

```csharp
// "이전 실행에서 생성한 아이템이 남아있으면 제거"라는 의도로 작성된 코드
Transform existingItem = fieldReward.transform.Find("Button - Item6");
if (existingItem != null)
    Object.Destroy(existingItem.gameObject);

// ... 이후 symbolCount > 0 일 때만 "Button - Item6" 재생성
```

---

## 조사 과정

### 시도 1 — FontLocalizer 크래시 가설

`LoadFieldComplete` Postfix에서 생성한 `Button - Item6` 안의 `TextMeshProUGUI`에 `FontLocalizer`가 붙어 있었다. `GameFieldDefaultUI.SetActive(false → true)` 시 `FontLocalizer.OnEnable`이 게임 내부 상태와 충돌해 크래시를 유발한다는 가설.

**대응**: `FieldSymbolCounter` MonoBehaviour를 `GameFieldDefaultUI`에 `AddComponent`해 `OnDisable`에서 `Button - Item6`을 파괴.

**결과**: 크래시 지속. 로그에서 `symbolCount=0`이므로 `Button - Item6` 자체가 애초에 생성되지 않았음이 확인됨 → FontLocalizer 가설 기각.

### 시도 2 — FieldSymbolCounter 컴포넌트 자체가 원인

`symbolCount=0`으로 `Button - Item6`이 없는 상태에서도 크래시가 발생한다는 사실에서, `GameFieldDefaultUI`에 추가한 커스텀 MonoBehaviour(`FieldSymbolCounter`)가 게임의 컴포넌트 순회 로직과 충돌한다는 가설.

**대응**: `FieldSymbolCounter` 제거, `ComponentHelper.CreateTMPro`(FontLocalizer 포함) 대신 `TextMeshProUGUI` 직접 생성으로 변경.

**결과**: 크래시 지속.

### 시도 3 — GachaResultUI.SetActive 타입 불일치

`GachaMacroUIEnablePatch`가 `GachaResultUI.SetActive(bool)`를 패치할 때, 해당 메서드가 공통 기반 클래스에 정의되어 있으면 `GameFieldDefaultUI.SetActive` 호출 시에도 Postfix가 발화할 수 있다. Postfix 파라미터가 `GachaResultUI __instance`로 선언되어 있으면 try-catch 외부에서 `InvalidCastException`이 발생한다는 가설.

**대응**: `GachaResultUI __instance` → `object __instance`로 변경, `if (!(__instance is GachaResultUI gachaUI)) return;` 타입 검사 추가.

**결과**: 크래시 지속.

### 원인 특정 — 진단 로그 추가

모든 패치에 Prefix 로그(`BEFORE — type=...`)를 추가한 뒤 크래시 재현. 결과: 로비→필드 전환 구간에서 **어떤 패치도 발화하지 않음**. 크래시는 우리 Postfix 코드가 아닌 게임 자체 코드에서 발생.

게임의 `Layout - FieldReward` 자식 목록을 로그로 출력:

```
[FieldUI] FieldReward children:
  | Image - Bg
  | Button - Item1
  | Button - Item2
  | Button - Item3
  | Object - Line
  | Button - Item4
  | Button - Item5
  | Button - Item6      ← 게임이 원래 가지고 있는 오브젝트
```

**`Button - Item6`은 게임의 기본 UI 오브젝트였다.**

`LoadFieldComplete`가 호출될 때마다 "우리가 이전에 만든 Item6을 정리한다"는 의도로 작성된 코드가 실제로는 **게임 오브젝트를 삭제**하고 있었다. `symbolCount=0`이면 재생성하지 않으므로, 게임의 `Button - Item6`은 영구 소멸. 로비→필드 복귀 시 게임 코드가 이 오브젝트를 참조하려다 크래시.

---

## 근본 원인

우리 커스텀 오브젝트 이름을 게임의 기존 오브젝트와 동일하게 사용(`"Button - Item6"`)해 이름 충돌 발생. 게임 오브젝트 이름 검색 시 충돌을 일으켜 게임 오브젝트를 파괴함.

---

## 수정 내용

**`GameFieldDefaultUIEnablePatch.cs`**

커스텀 오브젝트 이름을 게임과 겹치지 않는 고유 이름으로 변경:

```csharp
// 변경 전
const string ItemName = "Button - Item6";  // 게임 오브젝트와 이름 충돌

// 변경 후
public const string ItemName = "RBX_SymbolCount";  // 플러그인 고유 접두사 사용
```

**`SymbolRemovePatch.cs`**

`"Button - Item6"` 하드코딩 → `GameFieldDefaultUIEnablePatch.ItemName` 참조로 변경.

---

## 추가 수정 사항 (조사 과정에서 발견된 문제)

### GachaResultUI.SetActive 타입 안전성

패치 대상 메서드가 공통 기반 클래스에 정의된 경우, 동일 기반 클래스를 상속하는 다른 UI 인스턴스에서도 Postfix가 발화할 수 있다. Postfix 파라미터를 구체 타입으로 선언하면 try-catch 외부에서 캐스팅 실패가 발생할 수 있으므로, `object` + 타입 검사 패턴으로 방어적 처리.

```csharp
// 변경 전
private static void SetActive_Postfix(GachaResultUI __instance, bool __0)
{
    try { if (!__0) return; ... }
}

// 변경 후
private static void SetActive_Postfix(object __instance, bool __0)
{
    try
    {
        if (!__0) return;
        if (!(__instance is GachaResultUI gachaUI)) return;
        // 이후 gachaUI 사용
    }
}
```

---

## 최종 변경 파일

| 파일 | 변경 내용 |
|------|-----------|
| `Patches/GameFieldDefaultUIEnablePatch.cs` | 오브젝트 이름 `"Button - Item6"` → `"RBX_SymbolCount"`; FontLocalizer 없는 TMP 직접 생성으로 변경 |
| `Patches/SymbolRemovePatch.cs` | 이름 하드코딩 → `GameFieldDefaultUIEnablePatch.ItemName` 참조 |
| `Patches/GachaMacroUIEnablePatch.cs` | `SetActive_Postfix` 파라미터를 `object __instance`로 변경 + 타입 검사 추가 |
| `Components/FieldSymbolCounter.cs` | 삭제 (불필요) |
