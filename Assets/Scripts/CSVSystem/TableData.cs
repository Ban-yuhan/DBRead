using UnityEngine;
using System.Linq; //데이터를 쉽게 검색(FirstOrDefault 등)하기 위해 필요한 마이크로소프트 도구
using System.Collections.Generic;


namespace MyCSVSystem
{
    // CSV 파일 1개 전체의 데이터(여러 개의 RwoEntity)를 보관하는 서랍장 클래스
    public class TableData : MonoBehaviour
    {
        private Dictionary<string, RowEntity> _rows = new Dictionary<string, RowEntity>();

        public void AddRow(string id, RowEntity row)
        {
            _rows[id] = row;
        }


        //ID로 원하는 행 데이터를 즉시 찾아 꺼냄.
        public RowEntity GetRow(string id)
        {
            if (_rows.TryGetValue(id, out var row))
            {
                return row;
            }

            Debug.LogWarning($"[TableData] ID '[id]'에 해당하는 데이터를 찾을 수 없습니다.");

            return null;
        }


        //ID를 몰라도 '이름'이나 특정 컬럼의 값으로 행을 찾음. ex) FindRowByColumn("Name", "치즈버거")
        public RowEntity FindRowByColumn(string columnName, string value)
        {
            return _rows.Values.FirstOrDefault(r => r.Get<string>(columnName) == value);
            //FirstOrDefault : 조건에 맞는 첫 번째 값을 가져오고 없으면 에러가 아닌 기본값 null을 반환. Systme.Linq네임스페이스 기능.
        }

        public IEnumerable<RowEntity> AllRows => _rows.Values;
    }
}
