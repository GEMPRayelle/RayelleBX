# CharRecovery 자동 먹이기 버그 수정 기록

자동 먹이기 매크로(`CharRecoveryUIEnablePatch`)에서 발생한 버그 기록.

---

## 목차

1. [버그 1 — 결속 코스튬이 1개인 캐릭터에서 매크로 진행 불가](#버그-1--결속-코스튬이-1개인-캐릭터에서-매크로-진행-불가)

---

## 버그 1 — 결속 코스튬이 1개인 캐릭터에서 매크로 진행 불가

### 증상

결속 코스튬이 단일(1개)인 캐릭터에서 자동 먹이기 버튼을 클릭하면 아래 로그를 마지막으로 매크로가 진행되지 않는다:

```
[Info   : RayelleBX] CharRecoveryUI Patch Activated
[Info   : RayelleBX] MacroMenu Button Clicked
[Info   : RayelleBX] [UIHelper] 'connectCostumeButton1' Not Found
```

### 원인

`EatMacroLoop`의 코스튬 연결 단계가 항상 `CostumeConnectScrollItem1`(인덱스 1, 두 번째 코스튬) 클릭을 먼저 시도한다.

```csharp
// 변경 전 — 코스튬 2개를 전제로 고정된 순서
if (!ShouldContinue() || !StepClickCostumeItem1()) break;  // 1개 캐릭터는 여기서 break
yield return new WaitForSeconds(0.5f);
if (!ShouldContinue() || !StepClickCostumeConnectEnable()) break;
yield return new WaitForSeconds(0.5f);
if (!ShouldContinue() || !StepClickConnect()) break;
yield return new WaitForSeconds(0.5f);
if (!ShouldContinue() || !StepClickCostumeItem0()) break;
yield return new WaitForSeconds(0.5f);
if (!ShouldContinue() || !StepClickCostumeConnectEnable()) break;
```

결속 코스튬이 1개인 캐릭터는 팝업에 `CostumeConnectScrollItem1`이 존재하지 않아 `UIHelper.TryInvokeButton`이 `false`를 반환하고, `break`로 루프 전체가 종료된다.

### 수정 내용 (`CharRecoveryUIEnablePatch.cs`)

`StepClickConnect()`로 팝업을 연 직후, `UIHelper.IsExistAndActive`로 `CostumeConnectScrollItem1`의 존재 여부를 확인한다. 존재하면 기존 2코스튬 흐름을 실행하고, 없으면 `Item1` 블록 전체를 건너뛰고 `Item0`만 처리한다.

```csharp
// 변경 후
if (!ShouldContinue() || !StepClickConnect()) break;
yield return new WaitForSeconds(0.5f);

// 팝업이 열린 후 CostumeConnectScrollItem1 존재 여부로 코스튬 수를 판단
if (UIHelper.IsExistAndActive(
    "Singleton (DontDestroy)/AppManager/UI/CostumeConnectPopupUI(Clone)/.../CostumeConnectScrollItem1"))
{
    // 코스튬 2개 이상: Item1 → 연결 활성화 → 재연결 → Item0 → 연결 활성화
    if (!ShouldContinue() || !StepClickCostumeItem1()) break;
    yield return new WaitForSeconds(0.5f);
    if (!ShouldContinue() || !StepClickCostumeConnectEnable()) break;
    yield return new WaitForSeconds(0.5f);
    if (!ShouldContinue() || !StepClickConnect()) break;
    yield return new WaitForSeconds(0.5f);
}
// 코스튬 1개: Item1 블록 생략, Item0만 처리
if (!ShouldContinue() || !StepClickCostumeItem0()) break;
yield return new WaitForSeconds(0.5f);
if (!ShouldContinue() || !StepClickCostumeConnectEnable()) break;
yield return new WaitForSeconds(0.5f);
```

### 처리 분기 요약

| 결속 코스튬 수 | 흐름 |
|---|---|
| 2개 이상 | Connect → Item1 → Enable → Connect → Item0 → Enable |
| 1개 | Connect → Item0 → Enable |
