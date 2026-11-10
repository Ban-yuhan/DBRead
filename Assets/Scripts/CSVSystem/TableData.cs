using System;
using System.Collections.Generic;
using System.Globalization;
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
            if (!string.IsNullOrEmpty(id))
            {
                _rows[id] = row;
            }
        }

        /// <summary>
        /// 특정 ID 또는 Key를 가진 행이 존재하는지 확인합니다.
        /// </summary>
        public bool HasRow(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return TryGetRow(id, out _);
        }

        /// <summary>
        /// ID/Key에 해당하는 행을 안전하게 조회합니다.
        /// 1차로 Dictionary에서 검색하고, 없을 경우 id가 숫자이면 level 컬럼으로 2차 스마트 폴백 검색합니다.
        /// </summary>
        public bool TryGetRow(string id, out RowEntity row)
        {
            if (string.IsNullOrEmpty(id))
            {
                row = null;
                return false;
            }

            // 1차: Dictionary에서 검색
            if (_rows.TryGetValue(id, out row))
            {
                return true;
            }

            // 2차 스마트 폴백: id가 숫자인 경우, "level" 컬럼 값이 해당 id와 일치하는 행 검색
            if (int.TryParse(id.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericId))
            {
                foreach (var r in _rows.Values)
                {
                    if (r.TryGetStat<int>("level", out int levelVal) && levelVal == numericId)
                    {
                        row = r;
                        return true;
                    }
                }

                // 레거시 ID(10001~10015, 20001~20010 등) 호환 스마트 폴백
                int legacyLevel = -1;
                if (numericId >= 10001 && numericId <= 10099) legacyLevel = numericId - 10000;
                else if (numericId >= 20001 && numericId <= 20099) legacyLevel = numericId - 20000;

                if (legacyLevel > 0)
                {
                    foreach (var r in _rows.Values)
                    {
                        if (r.TryGetStat<int>("level", out int levelVal) && levelVal == legacyLevel)
                        {
                            row = r;
                            return true;
                        }
                    }
                }
            }

            row = null;
            return false;
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
