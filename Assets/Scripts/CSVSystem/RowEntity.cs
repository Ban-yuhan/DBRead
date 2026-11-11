using System;
using System.Collections.Generic;
using System.Globalization;
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
            if (TryFind(_data, key, out object value))
            {
                return TryConvert(value, out result);
            }

            result = default;
            return false;
        }

        public T GetStat<T>(string key, T defaultValue = default)
        {
            if (!TryFind(_data, key, out object value))
            {
                Debug.LogWarning($"[RowEntity] '{key}' 컬럼을 찾을 수 없어 기본값 '{defaultValue}'을(를) 사용합니다.");
                return defaultValue;
            }

            return TryConvert(value, out T result) ? result : defaultValue;
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

        public IEnumerable<string> GetColumns() => _data.Keys;

        public string GetColumnType(string columnName) =>
            _columnTypes.TryGetValue(columnName, out string type) ? type : "";

        public bool ContainsColumn(string columnName) => _data.ContainsKey(columnName);

        /// <summary>
        /// 정확한 이름으로 찾고, 없으면 언더스코어 차이를 무시하고 찾습니다. (ProcessTime == process_time)
        /// </summary>
        internal static bool TryFind(Dictionary<string, object> stats, string key, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(key)) return false;
            if (stats.TryGetValue(key, out value) && value != null) return true;

            string stripped = key.Replace("_", "");
            foreach (var kvp in stats)
            {
                if (kvp.Value != null && kvp.Key.Replace("_", "").Equals(stripped, StringComparison.OrdinalIgnoreCase))
                {
                    value = kvp.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        internal static bool TryConvert<T>(object value, out T result)
        {
            result = default;
            if (value is T direct)
            {
                result = direct;
                return true;
            }
            if (value == null) return false;

            try
            {
                Type type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                object converted;

                if (type.IsEnum)
                {
                    converted = value is string name
                        ? Enum.Parse(type, name.Trim(), true)
                        : Enum.ToObject(type, value);
                }
                else if (type == typeof(string))
                {
                    converted = Convert.ToString(value, CultureInfo.InvariantCulture);
                }
                else
                {
                    if (value is string text)
                    {
                        text = text.Trim();
                        if (text.Length == 0) return false;
                        if (type == typeof(float) || type == typeof(double)) text = text.TrimEnd('f', 'F', 'd', 'D');
                        if (type == typeof(bool) && (text == "1" || text == "0")) text = text == "1" ? "true" : "false";
                        value = text;
                    }
                    converted = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                }

                result = (T)converted;
                return true;
            }
            catch (Exception e) when (e is FormatException || e is InvalidCastException || e is OverflowException || e is ArgumentException)
            {
                return false;
            }
        }
    }
}
