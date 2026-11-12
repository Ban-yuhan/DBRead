# CSV Data System

CSV 테이블을 읽어 게임 오브젝트의 변수에 적용하는 패키지입니다. 기존 스크립트를 고치지 않고 수치를 CSV로 옮길 수 있습니다.

## 설치

이 폴더를 대상 프로젝트의 `Packages/` 안에 복사합니다. 외부 패키지 의존성은 없습니다.

## CSV 작성

`Assets/Resources/Data/`에 넣으면 시작할 때 전부 자동으로 로드됩니다. 테이블 이름은 파일 이름입니다.

```csv
key,level,process_time,capacity
string,int32,float,int32
Machine_A_1,1,2.5,6
Machine_A_2,2,1.8,10
```

- 1행은 컬럼 이름, 2행은 타입, 3행부터 데이터입니다.
- 타입은 `int`, `int32`, `int64`, `long`, `float`, `double`, `bool`, `string`을 지원합니다.
- A열은 행 ID이며 중복되면 안 됩니다.

## 변수에 적용하기

수치를 넣을 오브젝트에 컴포넌트 두 개를 추가합니다. `DatabaseManager`는 씬에 없어도 자동 생성됩니다.

**EntryCloner**

| 항목 | 값 | 예 |
|---|---|---|
| Target Table | CSV 파일 이름 | `FacilityData` |
| Row Key | 레벨 번호를 뺀 키 | `Machine_A` |
| Target ID | 시작 레벨 (Row Key가 없으면 행 ID) | `1` |

**StatBinder** 의 Bindings에 변수별로 한 줄씩 추가합니다.

| 항목 | 값 | 예 |
|---|---|---|
| Target | 값을 넣을 컴포넌트 | `BurgerMachine` |
| Member Name | 필드 또는 프로퍼티 이름 (`private` 가능) | `produceTime` |
| Column | CSV 컬럼 이름 | `process_time` |

`Start`에서 값을 다른 곳으로 복사하는 코드라면 복사 대상을 직접 연결합니다. 예를 들어 Target을 `NavMeshAgent`, Member Name을 `speed`로 지정합니다.

## 레벨업 연결

게임의 업그레이드 코드에 한 줄을 추가합니다. 값은 바인딩된 변수에 자동으로 다시 들어갑니다.

```csharp
int level = GetComponent<MyCSVSystem.EntryCloner>().UpgradeLevel();
```

다음 레벨의 행이 CSV에 없으면 레벨이 오르지 않으므로, 최대 레벨은 CSV가 결정합니다.

## 코드 수정이 필요한 경우

아래 값은 바인더로 바꿀 수 없어서 일반 변수로 고치거나 `GetStat`으로 읽어야 합니다.

- `const`, `readonly`, 지역 변수, 코드에 직접 쓴 숫자
- 값을 미리 계산해 둔 파생값 (`interval = 1f / speed`)

```csharp
speed = GetComponent<MyCSVSystem.EntryCloner>().GetStat<float>("speed", speed);
```

ScriptableObject를 대상으로 지정하면 에디터에서 에셋이 변경되므로 복사본을 사용하세요. IL2CPP 빌드에서는 바인딩이 동작하는지 한 번 확인하세요.

## ScriptableObject로 임포트하기 (1.1.0)

런타임에 CSV를 파싱하지 않고, 에디터에서 CSV 값을 ScriptableObject 에셋에 저장하는 방식입니다. 게임 코드는 에셋만 읽습니다.

1. 행 클래스를 `[Serializable]`로 만듭니다. 필드 이름은 컬럼 이름과 언더스코어·대소문자를 무시하고 맞춥니다 (`processTime` = `process_time`).
   - `[CsvColumn("fallback_cost")]` 다른 이름의 컬럼 연결
   - `[CsvOptional]` 컬럼이 없거나 칸이 비어도 기본값 유지 (없으면 오류)
   - `[CsvIgnore]` CSV에서 읽지 않는 필드
   - `List<T>`/`T[]` 필드는 `a|b|c` 형식, enum은 이름(대소문자 무시, 정의된 값만)으로 읽습니다.
   - 숫자 타입 컬럼의 빈칸은 지금처럼 0으로 읽습니다.
   - 상속받은 클래스의 private `[SerializeField]` 필드는 매핑하지 않습니다. 행 클래스는 public 필드로 만드세요.
2. 에셋 클래스를 만듭니다.
   ```csharp
   [CreateAssetMenu(menuName = "Data/Facility Table")]
   public class FacilityTable : CsvTableAsset<FacilityRow>
   {
       protected override string KeyOf(FacilityRow row) => row.key;   // TryGet(key)용
       protected override void Validate(IReadOnlyList<FacilityRow> rows, CsvImportContext ctx) { /* ctx.Error(...) */ }
   }
   ```
   CSV 여러 개를 합치는 에셋은 `CsvImportedAsset`을 상속해 `Sources`와 `OnImport(ctx)`를 구현합니다 (`ctx.Read`, `ctx.MapRows<T>`, `ctx.FindAsset<Sprite>`).
3. 에셋을 만들고 Source에 CSV를 연결한 뒤 인스펙터의 **CSV에서 다시 가져오기** 또는 메뉴 **Tools/CSV/Reimport All**.
4. 이후 CSV를 저장하면 그 CSV를 쓰는 에셋이 자동으로 다시 임포트됩니다. 자동 임포트를 놓친 경우(스크립트 리로드 중 저장, 에디터를 끈 채 수정)를 위해 에디터를 켤 때와 Play 직전에 CSV 해시를 비교해 어긋난 에셋을 다시 임포트합니다.

### 오류 처리

| 상황 | 에디터 임포트 (SO) | 런타임 `DatabaseManager` |
|---|---|---|
| 빈칸 | 타입 기본값 (0, false, "") — 오류 아님 | 같음 |
| 타입에 맞지 않는 값 (`int`에 `abc`), 없는 enum 이름, 닫히지 않은 따옴표, 중복 ID, 필수 열 없음 | **임포트 오류** | 경고 로그 후 기본값으로 진행 |

- 임포트 오류가 있으면 에셋 데이터는 **마지막 정상 임포트 값을 유지**하고, 오류만 콘솔·인스펙터·`ImportErrors`에 남습니다. 게임 쪽은 `HasImportErrors`로 시작을 막을 수 있습니다.
- 다시 임포트해도 결과가 같으면 에셋을 저장하지 않습니다 (VCS 변경 없음). `ImportedAt`은 데이터가 실제로 바뀐 시각입니다.

### 빌드 포함 여부

런타임에는 CSV를 파싱하지 않지만, 에셋의 Source 필드가 CSV(TextAsset)를 참조하므로 **그 에셋이 빌드에 들어가면 CSV 파일도 함께 포함됩니다** (Resources 밖에 있어도). 보통 크기가 작아 문제되지 않습니다.

## 저장

레벨은 메모리에만 유지되며 플레이를 종료하면 초기화됩니다. 저장하려면 게임 쪽에서 `CurrentLevel`을 저장하고 시작할 때 `SetLevel(level)`로 복원합니다.
