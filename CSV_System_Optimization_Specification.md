# [RFC Specification] 범용 CSV 데이터 시스템 UPM 패키지화 및 최적화 요청서

- **문서 버전**: v1.0.0
- **작성 목적**: 현재 Unity 프로젝트의 CSV 데이터 파싱 시스템을 **독립적인 UPM(Unity Package Manager) 패키지로 분리**하고, 타 프로젝트(GOS 등)로의 무결한 이식성과 고성능·안정성을 확보하기 위한 리팩토링 및 최적화 사양 정의.
- **적용 대상**: `Assets/Scripts/CSVSystem/` 전반 ➔ `com.company.csvsystem` UPM 패키지화

---

## 1. 개요 및 배경 (Background & Objectives)

### 1.1 현재 문제점
1. **도메인 종속성(Coupling)**: CSV 시스템 코드가 특정 프로젝트의 도메인 로직(`BurgerMachineTest`, `SaveManager`)과 강하게 결합되어 있어 타 프로젝트(GOS 등)로 직접 복사 이식 시 컴파일 에러가 발생함.
2. **치명적 런타임 크래시(Null-Safety 부재)**: 기획자가 CSV에 새 파라미터 컬럼을 추가하고 기존 행의 값을 빈칸(`,,`)으로 둘 경우, `EntryCloner.GetStat<T>`에서 `NullReferenceException` 또는 `InvalidCastException`이 발생하여 게임이 비정상 종료됨.
3. **사일런트 데이터 증발(Silent Row Drop)**: `if (values.Length < headers.Length) continue;` 로직으로 인해 엑셀 익스포트 시 끝부분 쉼표가 생략되면 에러 로그 없이 캐릭터/시설 데이터 행 전체가 영구 누락됨.
4. **RFC 4180 표준 미준수**: 단순 `string.Split(',')`을 사용하여, 대사나 설명 텍스트 내부의 콤마(`"공격력 10, 방어력 5"`) 또는 개행(`\n`)이 포함될 때 컬럼 인덱스가 완전히 붕괴됨.
5. **글로벌 환경 크래시(CultureInfo)**: `float.Parse` 사용 시 OS 언어 설정(유럽권 등 소수점을 콤마로 표기)에 따라 `FormatException` 발생.

### 1.2 패키지화 목표
- **완전 무의존성 (Zero Domain Dependency)**: 유니티 씬(Scene) 없이도 C# 단위 테스트 및 에디터 툴에서 동작하는 순수 POCO 코어 구축.
- **UPM 규약 준수**: `Packages/com.company.csvsystem` 형태로 Git URL 또는 로컬 패키지 매니저를 통해 한 번에 설치 및 업데이트 가능.
- **동적 유동성 및 관계형 지원**: GOS 프로젝트의 장점인 `struct`/`ref` 테이블 간 참조 병합을 흡수하되, 키 충돌(Key Collision)을 방어하는 네임스페이스 격리 제공.
- **저할당(Low-Alloc) 및 비동기 지원**: 가비지 컬렉션(GC Alloc)을 최소화하고 Addressables 및 비동기 다운로드 파이프라인 대응.

---

## 2. UPM 패키지 구조 사양 (Package Architecture)

### 2.1 디렉토리 레이아웃 표준
```text
Packages/com.company.csvsystem/
├── package.json                          # UPM 메타데이터 및 의존성 정의
├── README.md                             # 설치 및 빠른 시작 가이드
├── CHANGELOG.md                          # 릴리즈 변경 내역
├── Runtime/
│   ├── Company.CSVSystem.Runtime.asmdef  # 런타임 어셈블리 정의 (독립 어셈블리)
│   ├── Core/
│   │   ├── CSVDatabase.cs               # 순수 C# 코어 데이터베이스 (POCO)
│   │   ├── TableData.cs                 # 테이블 단위 데이터 보관소
│   │   ├── RowEntity.cs                 # 행 단위 키-값 보관 및 타입 안전 변환
│   │   └── CSVParser.cs                 # RFC 4180 호환 저할당 토크나이저
│   ├── Loaders/
│   │   ├── ICSVLoader.cs                # 동기 로더 추상화 인터페이스
│   │   ├── IAsyncCSVLoader.cs           # 비동기(Addressables/Web) 로더 인터페이스
│   │   ├── ResourcesCSVLoader.cs        # Unity Resources 기본 구현체
│   │   └── FileCSVLoader.cs             # 로컬 FileIO/StreamingAssets 구현체
│   └── Unity/
│       ├── DatabaseManager.cs           # 씬 레벨 MonoBehaviour 싱글톤 래퍼
│       └── EntryCloner.cs               # 런타임 게임오브젝트 스탯 바인딩 컴포넌트
├── Editor/
│   ├── Company.CSVSystem.Editor.asmdef   # 에디터 전용 어셈블리 (UnityEditor 참조)
│   └── CSVValidatorWindow.cs            # CSV 데이터 무결성 검증 에디터 윈도우
└── Tests/
    ├── Runtime/                         # 런타임 파싱 및 스탯 유닛 테스트 (NUnit)
    └── Editor/                          # RFC 4180 파서 엣지 케이스 테스트
```

### 2.2 package.json 명세
```json
{
  "name": "com.company.csvsystem",
  "version": "1.0.0",
  "displayName": "Universal CSV Data System",
  "description": "High-performance, RFC 4180 compliant, dynamic CSV database system for Unity.",
  "unity": "2021.3",
  "keywords": ["csv", "database", "data", "parser"],
  "author": {
    "name": "Your Company / Studio"
  }
}
```

---

## 3. 핵심 모듈별 최적화 및 구현 사양

### 3.1 코어 아키텍처 분리 (POCO Core vs Unity Component)
* **`CSVDatabase.cs` (POCO Core)**:
  * `MonoBehaviour`를 상속받지 않는 순수 C# 클래스.
  * 서버, 유닛 테스트, 빌드 전처리 스크립트 등 어디서든 독립 실행 가능.
* **`DatabaseManager.cs` (Unity Wrapper)**:
  * `CSVDatabase` 인스턴스를 씬 라이프사이클에서 관리하는 래퍼 컴포넌트.
  * 필요 시 `DontDestroyOnLoad` 싱글톤으로 인게임 브릿지 역할만 수행.

### 3.2 로더 인터페이스 추상화 (`ICSVLoader`, `IAsyncCSVLoader`)
특정 로딩 경로(Resources)에 대한 하드코딩을 제거하고 교체 가능한 인터페이스를 제공:
```csharp
namespace Company.CSVSystem.Runtime.Loaders
{
    public interface ICSVLoader
    {
        string LoadText(string path);
    }

    public interface IAsyncCSVLoader : ICSVLoader
    {
        System.Threading.Tasks.Task<string> LoadTextAsync(string path);
    }
}
```

---

### 3.3 파서(Parser) 최적화 및 RFC 4180 표준화

#### (1) RFC 4180 파서 도입
- 단순 `Split(',')`을 완전 폐기하고 **문자 단위 상태 머신(State Machine)** 기반의 토크나이저 채택.
- **요구 사양**:
  1. 큰따옴표 내부의 콤마(`"Item, Rare"`)를 구분자로 취급하지 않고 하나의 필드로 유지.
  2. 큰따옴표 내부의 개행문자(`\n`, `\r\n`)를 데이터로 유지.
  3. 이스케이프된 연속 큰따옴표(`""`)를 단일 큰따옴표(`"`)로 디코딩.

#### (2) 글로벌 문화권(CultureInfo) 및 접미사 방어
- `float`, `double` 파싱 시 `CultureInfo.InvariantCulture`를 강제 적용.
- 기획자가 오입력한 접미사(`1.5f`, `100F`) 및 공백(`" 3.5 "`) 자동 정제:
```csharp
case "float":
    string cleanFloat = value.Trim().TrimEnd('f', 'F');
    return float.TryParse(cleanFloat, NumberStyles.Float, CultureInfo.InvariantCulture, out float fVal) ? fVal : 0.0f;
```

---

### 3.4 데이터 무결성 및 Null-Safety 강화

#### (1) 사일런트 행 누락 방지 (Silent Row Drop 방지 및 자동 패딩)
- **위치**: `CSVDatabase.LoadTable`
- **구현 사양**: 컬럼 수가 헤더 수보다 부족한 행을 무시(`continue`)하지 않고, 빈 필드로 자동 패딩(Padding)하여 행 유실을 방지하고 에러/경고 로그를 출력.
```csharp
// 자동 패딩 사양
for (int j = 0; j < headers.Length; j++)
{
    string rawValue = (j < values.Length) ? values[j] : string.Empty;
    object parsedValue = CSVParser.ParseValue(types[j], rawValue);
    row.SetField(headers[j], parsedValue, types[j]);
}
```

#### (2) `EntryCloner` 및 `RowEntity` Null-Safety & Fallback 기본값
- `null` 조회 시 `(T)val` 언박싱에 의한 `NullReferenceException` 및 `InvalidCastException`을 원천 차단.
- C# 표준 관용구 `TryGetValue` 및 커스텀 `defaultValue` 오버로드 지원.
```csharp
public bool TryGetStat<T>(string key, out T result)
{
    if (_runtimeStats.TryGetValue(key, out object val) && val != null)
    {
        try 
        { 
            result = (T)Convert.ChangeType(val, typeof(T), CultureInfo.InvariantCulture); 
            return true;
        }
        catch 
        { 
            result = default; 
            return false; 
        }
    }
    result = default;
    return false;
}

public T GetStat<T>(string key, T defaultValue = default)
{
    return TryGetStat<T>(key, out T result) ? result : defaultValue;
}
```

---

### 3.5 관계형 데이터 유동성 (GOS 벤치마킹 & Key Collision 방어)

GOS 프로젝트의 `struct`/`ref` 기능을 지원하되, 서브 테이블 컬럼명이 메인 테이블 컬럼명을 덮어쓰는 **키 충돌(Key Collision)을 방지하는 네임스페이스 옵션**을 제공:
- **정책 열거형**:
```csharp
public enum StructMergePolicy
{
    FlattenWithPrefix, // "Class.HP", "Class.Atk" (권장: 키 충돌 방지)
    FlattenDirect,     // "HP", "Atk" (GOS 호환 모드, 중복 시 경고 로그)
    KeepNested         // 서브 RowEntity 객체 자체를 값으로 유지
}
```

---

## 4. 성능 및 가비지(GC Alloc) 최적화 사양

1. **저할당 스트리밍 파싱**:
   - 대형 CSV 파일을 `Split('\n')`으로 한 번에 수만 개의 string 배열로 쪼개지 않고, `StringReader` 또는 `ReadOnlySpan<char>`를 활용하여 가비지 생성을 기존 대비 80% 이상 감축.
2. **DTO 타입 바인딩 (Fast-Path) 지원**:
   - 매 프레임 `Update()`에서 딕셔너리 해시 탐색 및 언박싱 비용이 발생하지 않도록, 자주 쓰이는 핵심 스탯을 DTO 클래스로 한 번에 역직렬화하는 `Bind<T>()` 헬퍼 메서드 제공.

---

## 5. 단계별 마이그레이션 및 이식 가이드

### Step 1. 패키지 설치
1. `Assets/Scripts/CSVSystem/`을 `Packages/com.company.csvsystem/`으로 이동.
2. `package.json` 및 `asmdef` 정의.

### Step 2. 기존 프로젝트(`DBRead`) 조정
1. `BurgerMachineTest.cs` 및 `TestCSVLoader.cs`의 네임스페이스를 `using Company.CSVSystem.Runtime;`으로 업데이트.
2. `cloner.GetStat<float>("ProduceTime", defaultValue: 1.0f)` 형태로 기본값 인자 적용.

### Step 3. 타 프로젝트(`GOS-Lifeis`) 이식
1. GOS 프로젝트의 `Manifest.json`에 로컬 패키지 경로 추가:
   `"com.company.csvsystem": "file:../../Packages/com.company.csvsystem"`
2. 기존 GOS의 `CSVParser.cs`, `RowEntity.cs`, `EntryCloner.cs`를 패키지 코드로 대체.
3. `EntryModifierEvent.cs`에서 `targetCloner.GetStat<int>(statKey, defaultValue: 0)`으로 널 안전성 확보.

---

## 6. 검증 및 수락 기준 (Acceptance Criteria)

- [ ] **단위 테스트 통과**: 빈 셀(`,,`), 누락 행, 쌍따옴표 내부 콤마(`"a, b"`), 특수문자 파싱 테스트 100% 통과.
- [ ] **Zero Crash 보증**: 기획자가 CSV에 새 컬럼을 추가하고 빈칸으로 두더라도 런타임 NRE 및 CastException이 일절 발생하지 않을 것.
- [ ] **행 누락 방지**: 끝 컬럼 콤마가 누락된 불완전한 CSV를 로드해도 데이터 행이 누락되지 않고 자동 패딩될 것.
- [ ] **독립 빌드 검증**: 유니티 빈 프로젝트(Empty Project)에 UPM 패키지 추가 시 추가 의존성 없이 단번에 컴파일 및 테스트가 완료될 것.
