using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyCSVSystem
{
    public class RowEntity
    {
        private Dictionary<string, object> _data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _columnTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public void SetField(string columnName, object value, string columnType)
        {
            _data[columnName] = value;
            _columnTypes[columnName] = columnType;
        }

        public bool TryGetStat<T>(string key, out T result)
        {
            if (StatValue.TryFind(_data, key, out object value))
            {
                return StatValue.TryConvert(value, out result);
            }

            result = default;
            return false;
        }

        public T GetStat<T>(string key, T defaultValue = default)
        {
            return TryGetStat(key, out T result) ? result : defaultValue;
        }

        public T Get<T>(string columnName)
        {
            if (TryGetStat(columnName, out T result))
            {
                return result;
            }

            Debug.LogWarning($"[RowEntity] '{columnName}' 컬럼을 찾을 수 없습니다.");
            return default;
        }

        /// <summary>
        /// 변환하지 않은 셀 값을 찾습니다. 컬럼 이름은 언더스코어·대소문자 차이를 무시합니다.
        /// </summary>
        public bool TryGetRaw(string key, out object value) => StatValue.TryFind(_data, key, out value);

        public IEnumerable<string> GetColumns() => _data.Keys;

        public string GetColumnType(string columnName) =>
            _columnTypes.TryGetValue(columnName, out string type) ? type : "";

        public bool ContainsColumn(string columnName) => _data.ContainsKey(columnName);
    }
}
