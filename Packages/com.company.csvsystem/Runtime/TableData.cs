using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MyCSVSystem
{
    /// <summary>
    /// 단일 CSV 테이블의 전체 데이터(행들의 모음)를 관리하는 클래스. 행은 파일 순서를 유지합니다.
    /// </summary>
    public class TableData
    {
        private readonly Dictionary<string, RowEntity> _rows = new Dictionary<string, RowEntity>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _order = new List<string>();

        /// <summary>
        /// 행을 추가합니다. ID가 중복되면 뒤의 행으로 덮어쓰고 false를 반환합니다 (보고는 호출하는 쪽이 합니다).
        /// </summary>
        public bool AddRow(string id, RowEntity row)
        {
            if (string.IsNullOrEmpty(id)) return false;

            if (_rows.ContainsKey(id))
            {
                _rows[id] = row;
                return false;
            }

            _rows[id] = row;
            _order.Add(id);
            return true;
        }

        public int Count => _order.Count;

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
            return AllRows.FirstOrDefault(r => r.Get<string>(columnName) == value);
        }

        /// <summary>파일 순서의 행 ID</summary>
        public IReadOnlyList<string> RowIds => _order;

        /// <summary>파일 순서의 행</summary>
        public IEnumerable<RowEntity> AllRows => _order.Select(id => _rows[id]);
    }
}
