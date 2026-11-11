# CSV Data System

CSV 테이블을 읽어 키-값으로 조회하고, 레벨에 맞는 행의 값을 게임 오브젝트에 제공하는 패키지입니다.

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

## 테이블에서 직접 읽기

`DatabaseManager`는 씬에 없어도 자동 생성됩니다.

```csharp
var table = MyCSVSystem.DatabaseManager.Instance.GetTable("FacilityData");
if (table != null && table.TryGetRow("Machine_A_1", out var row))
    speed = row.GetStat("process_time", speed);   // 컬럼이 없으면 기존 값
```

## 오브젝트에 연결하기

값을 쓸 오브젝트에 `EntryCloner`를 추가합니다.

| 항목 | 값 | 예 |
|---|---|---|
| Target Table | CSV 파일 이름 | `FacilityData` |
| Row Key | 레벨 번호를 뺀 키 | `Machine_A` |
| Target ID | 시작 레벨 (Row Key가 없으면 행 ID) | `1` |

`EntryCloner`는 현재 레벨의 값을 보관합니다. 게임 코드에서 읽어 변수에 넣습니다.

```csharp
var cloner = GetComponent<MyCSVSystem.EntryCloner>();
produceTime = cloner.GetStat("process_time", produceTime);
```

## 레벨업

레벨을 올린 뒤 값을 다시 읽습니다.

```csharp
int level = cloner.UpgradeLevel();
produceTime = cloner.GetStat("process_time", produceTime);
```

다음 레벨의 행이 CSV에 없으면 레벨이 오르지 않으므로, 최대 레벨은 CSV가 결정합니다.

## 저장

레벨은 메모리에만 유지되며 플레이를 종료하면 초기화됩니다. 저장하려면 게임 쪽에서 `CurrentLevel`을 저장하고 시작할 때 `SetLevel(level)`로 복원합니다.
