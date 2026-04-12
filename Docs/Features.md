# RayelleBX 기능 문서

BrownDust 2용 BepInEx 플러그인 `RayelleBX`의 전체 기능 설명 및 설정 가이드.

---

## 목차

1. [기능 목록](#기능-목록)
2. [설치 및 배포](#설치-및-배포)
3. [setting.cfg 설정 가이드](#settingcfg-설정-가이드)
4. [기능별 상세 설명](#기능별-상세-설명)
   - [OverwhelmIndicator](#1-overwhelmindicator--압도-방향-인디케이터)
   - [심볼 카운터 UI](#2-심볼-카운터-ui)
   - [퀵메뉴 대화 매크로](#3-퀵메뉴-대화-매크로)
   - [자동 먹이기 매크로](#4-자동-먹이기-매크로)
   - [코스튬 ID 매핑 기록](#5-코스튬-id-매핑-기록)
   - [무한뽑기 매크로](#6-무한뽑기-매크로)
5. [리소스 파일](#리소스-파일)

---

## 기능 목록

| 기능 | setting.cfg 키 | 기본값 | 패치 파일 |
|------|---------------|--------|-----------|
| 압도 방향 인디케이터 | `OverwhelmIndicator` | true | `OverwhelmIndicatorPatch.cs` |
| 심볼 카운터 UI | (항상 활성화) | — | `GameFieldDefaultUIEnablePatch.cs` |
| 심볼 제거 업데이트 | (항상 활성화) | — | `SymbolRemovePatch.cs` |
| 퀵메뉴 대화 매크로 | `QuickMenuMacro` | true | `QuickMenuUIEnablePatch.cs` |
| 자동 먹이기 매크로 | `CharRecoveryMacro` | true | `CharRecoveryUIEnablePatch.cs` |
| 코스튬 ID 매핑 기록 | `CharCostumeLogging` | true | `CharUIEnablePatch.cs` |
| 무한뽑기 매크로 | `InfiniteGachaMacro` | true | `GachaMacroUIEnablePatch.cs` |

---

## 설치 및 배포

### 빌드 후 자동 배포

Visual Studio 또는 PowerShell MSBuild로 빌드하면 `DeployPlugin` Target이 자동으로 플러그인 폴더에 DLL을 복사한다.

```
배포 경로: C:\Neowiz\Browndust2\Browndust2_10000001\BepInEx\plugins\RayelleBX\
```

### 플러그인 폴더 구조

```
BepInEx\plugins\RayelleBX\
├── RayelleBX.dll           ← 플러그인 본체 (빌드 시 자동 복사)
├── setting.cfg             ← 기능 On/Off 및 매크로 설정 (최초 배포 시만 복사)
└── Resources\
    ├── symbol_monster.png  ← 심볼 카운터 아이콘
    ├── btn_normal.png      ← (예비, 현재 미사용)
    └── CostumeMapping.csv  ← 코스튬 ID·이름 매핑 DB (CharCostumeLogging이 누적 기록)
```

> `setting.cfg`는 이미 존재하면 빌드 시 덮어쓰지 않는다 (사용자 설정 보존).

---

## setting.cfg 설정 가이드

파일 위치: `BepInEx\plugins\RayelleBX\setting.cfg`

### 기능 On/Off

```ini
OverwhelmIndicator  = true   # 압도 방향 인디케이터
QuickMenuMacro      = true   # 대화 자동 스킵 매크로
CharRecoveryMacro   = true   # 자동 먹이기 매크로
CharCostumeLogging  = true   # 코스튬 ID CSV 기록
InfiniteGachaMacro  = true   # 무한뽑기 매크로
```

### 무한뽑기 매크로 설정

```ini
# 버튼 클릭 후 다음 동작까지 대기 시간 (초)
StepDelay          = 0.2

# 결과 화면 애니메이션 완료 대기 시간 (초)
WaitResultTimeout  = 1.0

# 10연차 결과에서 UR(5성)이 최소 몇 개여야 중단할지
MinUR              = 2

# 그 중 WishCostume 목록에 있는 코스튬이 최소 몇 개여야 할지
# 위시 코스튬 무관하게 MinUR만 보려면 EssentialUR = 0
EssentialUR        = 1

# 위시 코스튬 목록 (CostumeMapping.csv에서 ID 확인 후 작성)
WishCostume =
301,세헤라자드_푸른 마녀
302,이리야_성탄의 기적
```

**WishCostume 작성 규칙**:
- `WishCostume =` 줄 이후 한 줄에 하나씩 `CostumeID,이름` 형식
- 이름은 표시용이며 실제 판정은 CostumeID만 사용
- 다른 `키 = 값` 줄을 만나면 자동으로 목록 수집 종료

---

## 기능별 상세 설명

### 1. OverwhelmIndicator — 압도 방향 인디케이터

**동작**: 전투 필드에서 압도 스킬이 발동될 때, 각 몬스터 위에 빨간 방향 인디케이터를 표시한다.

**활성화 조건**: 전투 필드 (`GameField` 씬)

**중단 방법**: 인디케이터는 자동으로 소멸한다 (타이머 기반).

---

### 2. 심볼 카운터 UI

**동작**: 전투 필드 진입 시 화면 좌상단 보상 영역에 심볼 몬스터 남은 수를 표시한다. 몬스터가 제거될 때마다 카운터가 갱신되며, 모두 제거되면 UI가 사라진다.

**아이콘**: `Resources/symbol_monster.png`

---

### 3. 퀵메뉴 대화 매크로

**동작**: 퀵메뉴 하단 버튼 목록에 보라색 매크로 버튼이 추가된다. 클릭하면 대화창의 Skip 버튼을 자동으로 반복 클릭한다.

**중단 방법**:
- `Q` 키
- 오버레이의 빨간 종료 버튼

---

### 4. 자동 먹이기 매크로

**동작**: 캐릭터 회복 탭(CharRecoveryUI)에 "자동 먹이기" 버튼이 추가된다. 클릭하면 아래 순서를 자동으로 진행한다:

```
뒤로가기 → 연결 → 코스튬1 선택 → 코스튬 연결
→ 코스튬0 선택 → 코스튬 연결 → 회복 2회
→ 먹이기 아이템 클릭 N회 → 먹기
```

`N`은 `(최대체력 - 현재체력) / 2` 로 자동 계산된다.

**중단 방법**:
- `Q` 키
- 오버레이의 빨간 종료 버튼

---

### 5. 코스튬 ID 매핑 기록

**동작**: 코스튬 탭(CharCostumeUI)을 열 때마다 현재 보고 있는 코스튬의 ID와 이름을 `CostumeMapping.csv`에 기록한다.

**CSV 경로**: `Resources/CostumeMapping.csv`

**CSV 형식**:
```csv
CostumeID,CostumeName
301,세헤라자드_푸른 마녀
302,이리야_성탄의 기적
```

**용도**: 무한뽑기 매크로의 UR(5성) 판정 기준으로 사용된다.  
코스튬 탭을 한 번이라도 연 코스튬은 CSV에 기록되어 이후 뽑기 결과에서 해당 ID가 나오면 UR로 집계된다.

> **중복 기록 주의**: 같은 코스튬을 여러 번 열면 CSV에 여러 줄이 추가된다.  
> `LoadAllCostumes()`가 `HashSet<int>`로 반환하므로 중복 판정에는 영향 없다.

---

### 6. 무한뽑기 매크로

**동작**: 가챠 결과 화면(GachaResultUI)에 "자동 뽑기" 버튼이 생성된다 (다시 뽑기 버튼 옆).

**사용 방법**:
1. 수동으로 10연차 1회 실행
2. 결과 화면에 "자동 뽑기" 버튼 확인 후 클릭
3. 설정 조건을 만족할 때까지 자동으로 반복

**자동 루프 흐름**:
```
[다시 뽑기] 클릭
  → GachaInfinitePopupUI 팝업 대기
    → [YES] 클릭
      → [Skip] 버튼 반복 클릭 (애니메이션 스킵)
        → SetResult 대기 (결과 수신)
          → UR 수 / 위시코스튬 수 집계
            → 조건 충족? → 종료
            → 미충족? → 처음으로 돌아가 반복
```

**중단 조건**:
- `MinUR`개 이상의 UR이 나오고, 그 중 `EssentialUR`개 이상이 `WishCostume` 목록에 있을 때
- `Q` 키 누를 때 (즉시 중단)

**UR 판정 기준**: `CostumeMapping.csv`에 기록된 CostumeID = UR(5성)으로 간주  
→ 뽑기 전에 원하는 코스튬 탭을 미리 열어 CSV에 기록해두어야 한다.

**WishCostume ID 확인 방법**:
1. 원하는 코스튬의 캐릭터 코스튬 탭을 열기
2. `CostumeMapping.csv` 파일에서 해당 코스튬 ID 확인
3. `setting.cfg`의 `WishCostume` 항목에 추가

---

## 리소스 파일

| 파일 | 용도 | 비고 |
|------|------|------|
| `symbol_monster.png` | 심볼 카운터 아이콘 | 필드 UI에 표시 |
| `CostumeMapping.csv` | 코스튬 ID·이름 DB | CharCostumeLogging이 자동 기록 |

리소스 파일은 빌드 시 자동 복사되지 않는다. 최초 설치 또는 파일 삭제 시 직접 `Resources` 폴더에 배치해야 한다.
