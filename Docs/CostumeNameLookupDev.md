# CostumeNameLookup 개발 이력

`CharUIEnablePatch.cs`의 코스튬 이름 자동 기록 기능 구현 과정.  
게임 업데이트로 난독화 이름이 바뀌거나 동일한 문제가 재현됐을 때 참고한다.

---

## 목표

코스튬 탭을 열거나 코스튬 아이템을 선택할 때 **`{캐릭터명}_{코스튬명}`** 형태로 `CostumeMapping.csv`에 자동 누적 저장한다.  
예: `리베르타_미라클 로즈`

이 CSV는 `GachaMacroUIEnablePatch`의 UR 판정(`LoadAllCostumes`)에도 사용된다.

---

## 최종 구현 구조 (v2026-04-25 기준)

### 이름 조회 파이프라인

```
costumeData.Id (CostumeId)
    │
    ▼ Step1 — 로컬라이즈 타입 식별 (안전)
    costumeId / costumeId+1 로 int→string 전수 호출
    → 두 결과가 다르고 한국어인 타입 = _localizeType
    → (확인된 값) ὬὪὥὨὭὢὣὯὫὪὧ
    │
    ▼ Step2-a — CostumeTable 취득
    ὣὭὦὣὥὣὧὬὫὭὯ.ὯὮὮὢὬὨὨὥὦὫὫ(costumeId) → CostumeTable
    → CostumeTable.CostumeNameTextId (int, 예: 200383)
    → CostumeTable.UseUniqueCharId   (int, 예: 38)
    │
    ▼ Step2-b — 코스튬명 취득 (안전, _localizeType 내부만 호출)
    _localizeType 내 int→string 메서드에 CostumeNameTextId 전달
    → (확인된 메서드) ὭὮὣὯὤὢὮὧὭὦὤ(200383) = "미라클 로즈"
    │
    ▼ Step3 — 캐릭터명 취득
    UseUniqueCharId(38) 로 int→T 메서드 탐색
    → result.Id == 38 검증
    → int 프로퍼티(textId >= 10000)를 _localizeMethod로 시험
    → 짧은 한국어(≤10자, 줄바꿈/태그 없음) = 캐릭터명
    → (확인된 클래스) ὮὫὠὬὣὥὧὡὮὨὦ.ὡὬὯὨὮὩὡὡὡὠὤ
    │
    ▼ 조합
    "{캐릭터명}_{코스튬명}" → CostumeConfig.AppendMapping
```

### 난독화 이름 (v2026-04-23 ~ v2026-04-25 확인)

| 역할 | 난독화 이름 |
|------|------------|
| 로컬라이즈 클래스 | `ὬὪὥὨὭὢὣὯὫὪὧ` |
| 코스튬명 메서드 | `ὭὮὣὯὤὢὮὧὭὦὤ` (int→string) |
| CostumeTable 메서드 | `ὣὭὦὣὥὣὧὬὫὭὯ.ὯὮὮὢὬὨὨὥὦὫὫ` (int→CostumeTable) |
| CharCostumeUI costumeData 필드 | `ὥὪὩὢὣὯὩὨὮὫὢ` (CostumeDBInfo) |
| CharCostumeUI costumeList 필드 | `ὣὩὮὠὣὩὩὣὬὡὬ` (List\<T\>) |
| 캐릭터 테이블 메서드 | `ὮὫὠὬὣὥὧὡὮὨὦ.ὡὬὯὨὮὩὡὡὡὠὤ` (int→CharTable) |

---

## 개발 이력 및 에러 해결

### 시도 1 — TMP 텍스트에서 이름 읽기

**접근**: `ViewingTitle/Text - CostumeName - Viewing` TMP 컴포넌트에서 텍스트 직접 읽기.

**증상**: 모든 코스튬이 동일한 이름("라텔_약초 추격자") 저장.

**원인**: 해당 TMP는 "viewing(미리보기)" 모드에서만 갱신되는 stale 텍스트. 아이템을 클릭할 때는 아직 갱신되기 전 값이 들어 있음.

**폐기**.

---

### 시도 2 — int→string 전수 호출로 직접 코스튬명 취득

**접근**: 모든 타입의 `int→string` 정적 메서드에 `costumeId`를 직접 전달해 한국어 결과를 찾음.

**증상**: 올바른 이름이 나오지 않음.  
로그 예시:
```
(3803)='도움이 필요하다 했나?...'  (3804)='악마성 상점'
```

**원인**: costumeId(예: 3803)는 텍스트 테이블에서 게임 대사의 인덱스로 사용됨. 코스튬 이름이 아닌 대화 텍스트가 반환됨.

**교훈**: costumeId를 textId로 직접 쓸 수 없다. CostumeTable을 통해 `CostumeNameTextId`를 먼저 얻어야 한다.

---

### 시도 3 — CostumeDesignId로 CostumeTable 조회

**접근**: `CostumeDBInfo.CostumeDesignId`를 CostumeTable 키로 사용.

**증상**: `CostumeNameTextId 취득 실패`. 로그:
```
testDesignId=3892, CostumeNameTextId 취득 실패
```

**원인**: CostumeTable은 `CostumeId`(예: 3802)로 인덱싱됨. `CostumeDesignId`(예: 3892)와 다른 값임. `costumeData.Id` 프로퍼티가 CostumeId.

**수정**: `costumeData.CostumeDesignId` → `costumeData.Id` 사용.

---

### 시도 4 — textId로 전체 타입 탐색 시 팝업 발생

**접근**: `CostumeNameTextId`(예: 200383)를 모든 타입의 `int→string` 메서드에 전달해 한국어 결과를 찾음.

**증상**: 올바른 이름이 나왔지만 게임 UI에 팝업이 발생.  
로그:
```
[Error] [Func'1] Data not found exception. (CostumeDesignTable, id:200383)
```

**원인**: textId=200383을 `CostumeDesignTable.Get(200383)` 내부에서 조회하는 메서드가 있음. 200383은 CostumeDesignTable의 유효한 키가 아니므로 내부에서 에러 팝업을 표시함. C# 예외로 잡히기 전에 Unity UI 이벤트로 팝업이 먼저 나타남.

**원인 심층**: `dbTypes`(Table/Info 반환 타입을 가진 클래스) 필터링만으로는 부족했음. DB 조회 로직이 "Table/Info"를 반환하지 않는 타입에도 존재했음.

---

### 최종 팝업 해결 — 2단계 탐색 구조

**핵심 아이디어**: costumeId로 로컬라이즈 타입을 먼저 식별(Step1)하고, 그 타입 내 메서드만 textId로 호출(Step2). 다른 타입에는 textId를 절대 전달하지 않음.

**Step1 안전성**: costumeId(예: 3803)는 코스튬 테이블 클래스의 메서드에서는 유효한 키이므로 팝업 없음. 단, 이 단계에서 얻은 결과("도움이 필요하다 했나?...")는 이름이 아닌 대사 텍스트이므로 타입 식별 용도로만 사용.

**Step2 안전성**: `_localizeType` 내부 메서드만 호출. 이 클래스(`ὬὪὥὨὭὢὣὯὫὪὧ`)는 텍스트 조회 전용 클래스로 DB 팝업을 발생시키지 않음.

---

### 캐릭터명 추가 — 잘못된 프로퍼티 선택

**접근**: CostumeTable.UseUniqueCharId로 CharTable을 찾고, CharTable의 int 프로퍼티를 `_localizeMethod`로 시험해 짧은 한국어 이름을 찾음.

**증상 1**: 캐릭터명이 "미샤"로 나옴 (올바른 이름은 "리베르타").  
로그:
```
[CharUI] 캐릭터 테이블 확정: ὮὫὠὬὣὥὧὡὮὨὦ.ὡὬὯὨὮὩὡὡὡὠὤ, prop=TeamType, charName='미샤'
```

**원인**: `TeamType`은 소규모 enum 값(1~10 정도)인데, `_localizeMethod(teamTypeValue)`가 우연히 짧은 한국어 문자열 "미샤"를 반환함. 짧은 한국어 필터만으로는 팀 타입 enum을 걸러낼 수 없었음.

**수정**: `textId < 10000` 스킵 추가. 코스튬명 textId가 200000대임을 감안할 때 enum 값(< 10000)과 명확히 구분됨.

**증상 2**: 캐릭터명 탐색 중 팝업 발생.

**원인**: `FindCharTableMethod`가 코스튬 테이블 클래스(`_costumeTableMethod.DeclaringType`)의 메서드도 charId=38로 호출함. 코스튬 테이블 클래스의 다른 메서드들이 costumeId 범위(3000+)를 기대하므로 38이 들어오면 내부 DB 팝업 발생.

**수정**: `_costumeTableMethod.DeclaringType` 타입을 탐색에서 스킵.

---

## 현재 미해결 / 확인 필요 사항

| 항목 | 상태 | 설명 |
|------|------|------|
| TeamType 필터 충분성 | **미확인** | `textId < 10000` 필터 후 올바른 캐릭터명이 나오는지 실게임 로그 대기 중 |
| 캐릭터명 팝업 완전 제거 | **미확인** | 코스튬 테이블 클래스 스킵 후에도 다른 클래스에서 팝업 발생 가능성 있음 |
| `_charScanned` 1회 보장 | **OK** | `TryGetCharName`에서 `_charScanned=true` 선행 설정 후 호출 → 재탐색 없음 |
| `_scanned` 1회 보장 | **OK** | `TryGetCostumeName`에서 동일 패턴 적용 |

---

## 게임 업데이트 후 난독화 이름 변경 시 대응

### 자동 탐색 (런타임)

`FindNameMethod` / `FindCharTableMethod`는 매 게임 실행 첫 코스튬 탭 오픈 시 자동으로 난독화 이름을 재탐색한다. BepInEx 로그에서 다음 태그로 확인:

```
[DesignDB] 로컬라이즈 타입: {타입명} ...
[DesignDB] CostumeTable: {클래스명}.{메서드명} ...
[DesignDB] 이름 메서드 확정: ...
[CharUI] 캐릭터 테이블 확정: {클래스명}.{메서드명}, prop={프로퍼티명}, charName=...
```

### CLAUDE.md 업데이트

탐색 결과로 새 난독화 이름이 확인되면 `CLAUDE.md`의 "난독화 필드" 섹션을 갱신한다. DiagnosticHelper도 동일하게 갱신.

### DiagnosticHelper

`Helpers/DiagnosticHelper.cs` → `RunStartupDiagnostics()`가 플러그인 로드 시 기존 난독화 이름의 유효성을 BepInEx 로그에 출력한다. `[Diagnostic]` 태그로 검색.

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Patches/CharUIEnablePatch.cs` | 핵심 패치. 코스튬/캐릭터명 취득 전체 로직 |
| `Config/CostumeConfig.cs` | CSV upsert (`AppendMapping`), 로드 (`LoadAllCostumes`) |
| `Helpers/DiagnosticHelper.cs` | 게임 업데이트 후 난독화 이름 검증 도구 |
| `Docs/CostumeMapping.md` | 수동 기록된 코스튬 ID ↔ 이름 참조표 |
| `Docs/FontLocalizerReverseEngineering.md` | FontLocalizer 난독화 이름 탐색 방법론 |
