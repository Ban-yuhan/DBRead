# 🛠️ CSV 데이터 시스템 코드 리팩토링 상세 명세서 (Code Refactoring Specification)

- **문서 버전**: v1.0
- **작성 목적**: 기획팀과 개발팀 간 시스템 이해도 일치 및 CSV 파싱·세이브 파이프라인의 구조적 결함 해결을 위한 스크립트별 수정 명세 정리.
- **적용 대상**: Unity 클라이언트 프로젝트 내 CSV 파싱 및 인게임 데이터 연동 스크립트 6종

---

## 📌 1. 리팩토링 요약 및 종합 현황표

| 번호 | 스크립트 파일 | 핵심 문제점 | 주요 수정 내용 | 기대 효과 |
| :---: | :--- | :--- | :--- | :--- |
| **1** | `DatabaseManager.cs` | • `Awake` 순서에 따른 `NullReferenceException`<br>• 동일 테이블 중복 로드 시 자원 낭비 | • 프로퍼티 기반 Lazy 초기화(`FindFirstObjectByType`) 구현<br>• `LoadTable` 캐시 확인 및 중복 로드 방지 | 씬 로딩 순서에 무관한 안정성 보장 및 메모리·I/O 절약 |
| **2** | `EntryCloner.cs` | • `maxLevel = 10` 하드코딩으로 테이블 불일치 크래시<br>• `RowEntity`와의 130줄 타입 변환 중복 코드 | • 테이블 내 다음 레벨 존재 여부 동적 검증<br>• 공통 타입 변환 유틸리티로 로직 이관 | 기획 테이블의 레벨 상한 자동 추종 및 코드 중복 해소 |
| **3** | `SaveManager.cs` | • `GetLevel()` 호출 시마다 무조건 `File.WriteAllText` 실행<br>• 선형 탐색(`List.Find`) 오버헤드 | • 조회 시 디스크 쓰기 제거, 변경 시에만 I/O 수행<br>• 딕셔너리(`Dictionary`) 캐싱을 통한 $O(1)$ 탐색 | 게임 시작 시 프레임 드랍 및 디스크 병목 원천 제거 |
| **4** | `RowEntity.cs` | • `EntryCloner`와 복사-붙여넣기 수준의 타입 변환 로직 중복 | • 공통 데이터 변환 헬퍼(`DataValueConverter`)로 일원화 | 단일 책임 원칙(SRP) 준수 및 버그 픽스 누락 방지 |
| **5** | `TestCSVLoader.cs` | • 중복 테이블 로딩으로 메모리 낭비<br>• CP949(EUC-KR) 인코딩으로 인한 컴파일/Git 충돌 | • 스크립트 비활성화 또는 안전 삭제<br>• UTF-8 인코딩 통일 | 불필요한 수동 로딩 제거 및 인코딩 무결성 확보 |
| **6** | `BurgerMachineTest.cs` | • 최대 레벨 도달 후에도 무의미한 업그레이드 호출<br>• 반환값 검증 부재로 경고 로그 다량 발생 | • 업그레이드 성공 여부 반환값 검증 및 방어 로직 추가 | 불필요한 이벤트 호출 차단 및 콘솔 로그 오염 방지 |

---

## 🔍 2. 스크립트별 상세 수정 명세

---

### 1) `Assets/Scripts/CSVSystem/DatabaseManager.cs`

#### 🔴 현상 및 문제점 분석
1. **`Awake` 실행 순서 의존성 (`Script Execution Order`)**:  
   - 타 컴포넌트(예: `BurgerMachineTest`, `EntryCloner`)의 `Awake`나 `Start`에서 `DatabaseManager.Instance`를 참조할 때, `DatabaseManager`의 `Awake`가 먼저 호출되지 않으면 `NullReferenceException`이 발생합니다.
2. **`LoadTable` 중복 로딩 낭비**:  
   - 이미 메모리에 로드된 테이블임에도 불구하고 다시 호출되면 Resources에서 텍스트를 재로딩하고 파싱하여 인스턴스를 덮어씁니다.

#### 🛠️ 상세 수정 사항
- **Lazy Singleton 프로퍼티 구현**: `Instance` 접근 시 인스턴스가 `null`이면 씬에서 `FindFirstObjectByType<DatabaseManager>()`를 통해 동적으로 탐색하거나 자동 생성합니다.
- **테이블 로딩 중복 체크**: `LoadTable` 호출 시 `_tables.ContainsKey(tableName)`를 확인하여 이미 로드된 경우 조기 반환(`return`) 처리합니다.

```csharp
// [Before]
public static DatabaseManager Instance { get; private set; }

// [After] Lazy Singleton 패턴 적용
private static DatabaseManager _instance;
public static DatabaseManager Instance
{
    get
    {
        if (_instance == null)
        {
            _instance = FindFirstObjectByType<DatabaseManager>();
            if (_instance == null)
            {
                GameObject go = new GameObject("@DatabaseManager");
                _instance = go.AddComponent<DatabaseManager>();
                DontDestroyOnLoad(go);
            }
        }
        return _instance;
    }
}

public void LoadTable(string tableName, string resourcePath)
{
    // 이미 로드된 테이블이면 중복 파싱 방지
    if (_tables.ContainsKey(tableName)) return;
    
    // ... 파싱 및 로드 진행 ...
}
```

#### 🎯 수정 사유 및 기대 효과
- **초기화 타이밍 이슈 해결**: 다른 스크립트가 언제 호출하든 안전하게 데이터베이스가 초기화되므로 씬 전환 및 스크립트 실행 순서 버그가 원천 차단됩니다.
- **리소스 낭비 방지**: 씬 전환 시 반복적인 CSV 재파싱을 방지하여 로딩 성능이 향상됩니다.

---

### 2) `Assets/Scripts/CSVSystem/EntryCloner.cs`

#### 🔴 현상 및 문제점 분석
1. **최대 레벨 10 하드코딩 (`maxLevel = 10`)**:  
   - 실제 기획 데이터인 `FacilityData.csv`는 대부분 3레벨까지만 존재합니다.
   - 플레이어가 4레벨로 업그레이드를 시도하면 `SaveManager`는 4레벨로 올라가지만, 테이블에는 `Stage_1_BurgerMachine_1_4`가 존재하지 않아 `InitFromDB`가 실패하고 스탯이 갱신되지 않은 채 레벨과 스탯 간 불일치 버그가 발생합니다.
2. **`RowEntity.cs`와의 타입 변환 로직 중복**:  
   - `TryGetStat<T>`, `strippedKey`, CultureInfo 파싱 등 약 130줄에 달하는 복잡한 타입 파싱 코드가 `RowEntity`와 토씨 하나 안 틀리고 중복되어 있습니다.

#### 🛠️ 상세 수정 사항
- **동적 최대 레벨 판별 및 다음 레벨 유효성 검사**:  
  - `UpgradeLevel` 호출 시 테이블에 다음 레벨의 키(`{EffectiveSaveKey}_{nextLevel}`)가 실존하는지 먼저 검사한 후 업그레이드를 진행하도록 방어합니다.
- **타입 변환 로직 유틸리티 분리**:  
  - 자체 중복 로직을 제거하고 공통 유틸리티 클래스(`CSVValueConverter.TryConvert<T>`)를 호출하도록 간소화합니다.

```csharp
// [Before]
public int UpgradeLevel(int maxLevel = 10)
{
    int newLevel = SaveManager.Instance.UpgradeLevel(EffectiveSaveKey, maxLevel);
    string rowId = ResolveRowIdForLevel(newLevel);
    InitFromDB(targetTable, rowId);
    return newLevel;
}

// [After] 다음 레벨 데이터 존재 여부 사전 체크
public int UpgradeLevel()
{
    if (SaveManager.Instance == null) return 1;

    int currentLevel = SaveManager.Instance.GetLevel(EffectiveSaveKey, 1);
    int nextLevel = currentLevel + 1;
    string nextRowId = ResolveRowIdForLevel(nextLevel);

    var table = DatabaseManager.Instance?.GetTable(targetTable);
    if (table == null || !table.HasRow(nextRowId))
    {
        Debug.LogWarning($"[{gameObject.name}] 테이블({targetTable})에 다음 레벨({nextLevel}) 데이터가 없어 업그레이드 불가!");
        return currentLevel; // 현재 레벨 유지
    }

    // 다음 레벨 데이터가 확실히 있을 때만 세이브 갱신
    int finalLevel = SaveManager.Instance.SetLevel(EffectiveSaveKey, nextLevel);
    InitFromDB(targetTable, nextRowId);
    return finalLevel;
}
```

#### 🎯 수정 사유 및 기대 효과
- **기획 밸런스 변경에 유연한 대응**: 기획자가 CSV에서 최대 레벨을 3개, 5개, 10개로 수정하더라도 코드를 일일이 고칠 필요 없이 테이블 데이터에 맞춰 자동으로 한계가 제어됩니다.
- **데이터 불일치 방지**: 데이터가 없는 레벨로 넘어가는 오류를 차단합니다.

---

### 3) `Assets/Scripts/CSVSystem/SaveManager.cs`

#### 🔴 현상 및 문제점 분석
1. **조회(`GetLevel`) 시 무조건 디스크 쓰기(`File.WriteAllText`) 실행**:  
   - `GetLevel` 메서드는 데이터를 단순히 읽어오는 조회 함수임에도 불구하고, 신규 엔트리 생성 시 `Save()`를 호출하여 모바일/PC 디스크 I/O를 직접 유발합니다.
   - 씬에 30개의 설비 오브젝트가 배치되어 있다면, 게임 시작 1프레임 동안 디스크 파일 쓰기가 30회 연속 발생하여 심각한 렉(Spike Frame Drop)을 초래합니다.
2. **리스트 선형 탐색 (`O(N)`)**:  
   - `facilityLevels.Find(e => e.key == key)`로 순차 탐색을 수행하므로 설비 수가 늘어날수록 성능이 저하됩니다.

#### 🛠️ 상세 수정 사항
- **조회와 저장의 명확한 분리**:  
  - `GetLevel()`은 메모리 상에서만 조회/기본값 추가를 수행하고 디스크 저장(`Save()`)은 호출하지 않습니다.
  - 실제 값이 변경되는 `SetLevel`, `UpgradeLevel` 시점에만 `Save()`를 호출합니다.
- **런타임 딕셔너리 캐싱**:  
  - 런타임에는 빠른 탐색을 위해 `Dictionary<string, int>`로 관리하고, 파일 저장/로드 시에만 `List<LevelEntry>`로 직렬화/역직렬화합니다.

```csharp
// [Before] 조회 시마다 디스크 I/O 발생
public int GetLevel(string key, int defaultLevel = 1)
{
    var entry = data.facilityLevels.Find(e => e.key == key);
    if (entry != null) return entry.level;

    data.facilityLevels.Add(new LevelEntry(key, defaultLevel));
    Save(); // ⚠️ 매 조회 시마다 파일 쓰기 실행! (심각한 병목)
    return defaultLevel;
}

// [After] 조회 시에는 메모리 캐시만 반환
public int GetLevel(string key, int defaultLevel = 1)
{
    if (_levelCache.TryGetValue(key, out int level))
    {
        return level;
    }

    _levelCache[key] = defaultLevel;
    // 디스크 저장은 씬 시작 시점이 아닌 값 변경 시점으로 유예
    return defaultLevel;
}
```

#### 🎯 수정 사유 및 기대 효과
- **프레임 드랍 제거**: 게임 시작 및 씬 진입 시 디스크 I/O 병목이 100% 제거되어 쾌적한 로딩 속도를 보장합니다.
- **탐색 속도 개선**: $O(N)$ 선형 탐색에서 $O(1)$ 해시맵 탐색으로 전환되어 런타임 성능이 최적화됩니다.

---

### 4) `Assets/Scripts/CSVSystem/RowEntity.cs`

#### 🔴 현상 및 문제점 분석
- **중복 변환 코드의 파편화**:  
  - 언더스코어 제거 매칭(`ProduceTime` <-> `process_time`), `float.TryParse` (인라인 접미사 'f' 제거 및 invariant 문화권 처리), `bool` 변환(`"1"`, `"0"`, `"true"`) 등의 로직이 `RowEntity.cs`와 `EntryCloner.cs` 두 곳에 복제되어 관리되고 있습니다.
  - 추후 새로운 타입 지원이나 버그 수정 시 한쪽만 고치고 다른 쪽을 빠뜨리는 "코드 드리프트(Code Drift)" 위험이 매우 높습니다.

#### 🛠️ 상세 수정 사항
- **`DataValueConverter` 유틸리티 정적 클래스 추출**:  
  - 파싱 및 캐스팅 공통 로직을 전담 클래스로 캡슐화하고, `RowEntity`와 `EntryCloner`는 해당 클래스의 단일 메서드를 호출하도록 단순화합니다.

```csharp
// 신규 공통 유틸리티 생성
public static class DataValueConverter
{
    public static bool TryConvert<T>(object value, out T result)
    {
        // 문화권 안전(CultureInfo.InvariantCulture) 파싱 로직 집약
    }
}

// RowEntity.cs 및 EntryCloner.cs는 1줄로 위임
public bool TryGetStat<T>(string key, out T result)
{
    if (TryGetRawValue(key, out object raw))
    {
        return DataValueConverter.TryConvert<T>(raw, out result);
    }
    result = default;
    return false;
}
```

#### 🎯 수정 사유 및 기대 효과
- **단일 책임 원칙 (SRP) 준수**: 데이터 저장 구조(`RowEntity`)와 값 파싱/변환 로직이 명확히 분리됩니다.
- **유지보수성 향상**: 타입 변환 규칙 수정 시 단 한 곳의 파일만 수정하면 전 시스템에 일괄 반영됩니다.

---

### 5) `Assets/Scripts/TestCSVLoader.cs`

#### 🔴 현상 및 문제점 분석
1. **불필요한 중복 로딩**:  
   - `DatabaseManager.Awake()`에서 이미 `LoadAllTablesInFolder("Data")`를 통해 모든 CSV를 일괄 로드하고 있습니다.
   - `TestCSVLoader`가 별도로 `FacilityData`를 다시 로드하는 것은 명백한 중복이며, 컴포넌트 실행 순서에 따라 예외를 유발할 수 있습니다.
2. **인코딩 문제 (CP949)**:  
   - 해당 파일이 Windows ANSI(CP949) 인코딩으로 저장되어 있어 macOS/Linux 개발자 환경이나 CI/CD 빌드 머신, 그리고 Git 등에서 인코딩 깨짐 및 바이너리 오인식 문제가 발생합니다.

#### 🛠️ 상세 수정 사항
- **해당 스크립트 삭제 또는 비활성화 (`#if UNITY_EDITOR` 테스트 전용 래퍼로 격리)**
- **파일 인코딩 UTF-8 전환**: 프로젝트 표준인 UTF-8 (BOM 없음)으로 인코딩을 통일합니다.

#### 🎯 수정 사유 및 기대 효과
- **런타임 오버헤드 제거**: 시작 시 불필요하게 동일 데이터를 2번 파싱하는 낭비를 방지합니다.
- **크로스 플랫폼 안정성 확보**: 팀원 간 개발 환경 차이에 의한 한글 주석 깨짐 및 빌드 오류를 방지합니다.

---

### 6) `Assets/Scripts/BurgerMachineTest.cs`

#### 🔴 현상 및 문제점 분석
- **최대 레벨 도달 시 방어 로직 부재**:  
  - 스페이스바를 누를 때마다 무조건 `cloner.UpgradeLevel()`을 호출하고 반환값에 관계없이 `RefreshStats()`를 계속 실행합니다.
  - 최고 레벨에 도달했음에도 불필요한 스탯 재조회 및 디버그 로그가 매 프레임 발생할 수 있습니다.

#### 🛠️ 상세 수정 사항
- **레벨 상승 여부 조건문 분기**:  
  - 이전 레벨과 반환된 새 레벨을 비교하여 실제로 레벨이 올라갔을 때만 `RefreshStats()` 및 성공 로그를 출력하도록 개선합니다.

```csharp
// [Before]
int newLevel = cloner.UpgradeLevel();
RefreshStats();
Debug.Log($"[{gameObject.name}] 업그레이드 반영 완료! Level: {newLevel}...");

// [After]
int currentLevel = cloner.CurrentLevel;
int updatedLevel = cloner.UpgradeLevel();

if (updatedLevel > currentLevel)
{
    RefreshStats();
    Debug.Log($"<color=green>[{gameObject.name}] 업그레이드 성공! (Lv.{currentLevel} -> Lv.{updatedLevel})</color>");
}
else
{
    Debug.Log($"<color=yellow>[{gameObject.name}] 이미 최대 레벨(Lv.{currentLevel})이거나 업그레이드할 수 없습니다.</color>");
}
```

#### 🎯 수정 사유 및 기대 효과
- **불필요한 연산 방지**: 최대 레벨 상태에서 스페이스바를 연타하더라도 불필요한 딕셔너리 재탐색을 방지합니다.
- **정확한 UX 피드백**: 기획 및 QA 테스트 시 업그레이드 성공/실패 여부를 콘솔에서 명확히 구분할 수 있습니다.

---

## 🚀 3. 기대 종합 효과 및 향후 개발 권장 사항

1. **안정성 (Stability)**:  
   - 싱글톤 `Awake` 순서 꼬임으로 인한 크래시 및 최대 레벨 불일치로 인한 스탯 미갱신 현상이 100% 해소됩니다.
2. **성능 (Performance)**:  
   - 게임 시작 시 `SaveManager`의 다중 `File.WriteAllText` 병목을 제거하여 모바일 저사양 기기에서도 끊김 없는 씬 진입을 보장합니다.
3. **기획-개발 협업 효율 (Productivity)**:  
   - 기획자가 CSV 파일의 수치나 최대 레벨을 자유롭게 조율해도 코드가 능동적으로 적응하므로 개발팀에 레벨 상한 수정을 요청할 필요가 없습니다.
