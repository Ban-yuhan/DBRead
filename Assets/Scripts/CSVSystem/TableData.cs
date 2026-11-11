using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MyCSVSystem
{
    /// <summary>
    /// 단일 CSV 테이블의 전체 데이터(행들의 모음)를 관리하는 클래스
    /// </summary>
    public class TableData
    {
        private Dictionary<string, RowEntity> _rows = new Dictionary<string, RowEntity>(StringComparer.OrdinalIgnoreCase);

        public void AddRow(string id, RowEntity row)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (_rows.ContainsKey(id))
            {
                Debug.LogWarning($"[TableData] 중복된 ID '{id}'가 있어 앞의 행을 덮어씁니다.");
            }
            _rows[id] = row;
        }

        /// <summary>
        /// 특정 ID 또는 Key를 가진 행이 존재하는지 확인합니다.
        /// </summary>
        public bool HasRow(string id) => TryGetRow(id, out _);

        /// <summary>
        /// A열의 ID/Key와 정확히 일치하는 행을 조회합니다. (대소문자 무시)
        /// </summary>
        public bool TryGetRow(string id, out RowEntity row)
        {
            if (string.IsNullOrEmpty(id))
            {
                row = null;
                return false;
            }

            return _rows.TryGetValue(id.Trim(), out row);
        }

        /// <summary>
        /// ID/Key에 해당하는 행을 조회합니다. 없으면 경고 로그를 출력하고 null을 반환합니다.
        /// </summary>
        public RowEntity GetRow(string id)
        {
            if (TryGetRow(id, out var row))
            {
                return row;
            }

            Debug.LogWarning($"[TableData] ID '{id}'에 해당하는 데이터를 찾을 수 없습니다.");
            return null;
        }

        /// <summary>
        /// 특정 컬럼의 값으로 행을 검색합니다. ex) FindRowByColumn("facility_name", "버거 조리대 1")
        /// </summary>
        public RowEntity FindRowByColumn(string columnName, string value)
        {
            return _rows.Values.FirstOrDefault(r => r.Get<string>(columnName) == value);
        }

        public IEnumerable<RowEntity> AllRows => _rows.Values;
    }
}
