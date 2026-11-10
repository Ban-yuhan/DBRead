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
            if (string.IsNullOrEmpty(key))
            {
                result = default;
                return false;
            }

            if (!_data.TryGetValue(key, out object value) || value == null)
            {
                // Fallback: 언더스코어 유무 차이 자동 보정
                string strippedKey = key.Replace("_", "");
                foreach (var kvp in _data)
                {
                    if (kvp.Key.Replace("_", "").Equals(strippedKey, StringComparison.OrdinalIgnoreCase))
                    {
                        value = kvp.Value;
                        break;
                    }
                }

                if (value == null)
                {
                    if (key.Equals("ProduceTime", StringComparison.OrdinalIgnoreCase))
                    {
                        _data.TryGetValue("process_time", out value);
                    }
                    else if (key.Equals("process_time", StringComparison.OrdinalIgnoreCase))
                    {
                        _data.TryGetValue("ProduceTime", out value);
                    }
                }

                if (value == null)
                {
                    result = default;
                    return false;
                }
            }

            if (value is T directValue)
            {
                result = directValue;
                return true;
            }

            try
            {
                Type targetType = typeof(T);
                Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                if (underlyingType.IsEnum)
                {
                    if (value is string strVal)
                    {
                        result = (T)Enum.Parse(underlyingType, strVal, true);
                        return true;
                    }
                    result = (T)Enum.ToObject(underlyingType, value);
                    return true;
                }

                if (underlyingType == typeof(string))
                {
                    result = (T)(object)value.ToString();
                    return true;
                }

                if (value is string s)
                {
                    s = s.Trim();
                    if (string.IsNullOrEmpty(s))
                    {
                        result = default;
                        return false;
                    }

                    if (underlyingType == typeof(float))
                    {
                        if (float.TryParse(s.TrimEnd('f', 'F'), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float f))
                        {
                            result = (T)(object)f;
                            return true;
                        }
                    }
                    else if (underlyingType == typeof(double))
                    {
                        if (double.TryParse(s.TrimEnd('f', 'F', 'd', 'D'), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double d))
                        {
                            result = (T)(object)d;
                            return true;
                        }
                    }
                    else if (underlyingType == typeof(int))
                    {
                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                        {
                            result = (T)(object)i;
                            return true;
                        }
                    }
                    else if (underlyingType == typeof(long))
                    {
                        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                        {
                            result = (T)(object)l;
                            return true;
                        }
                    }
                    else if (underlyingType == typeof(bool))
                    {
                        if (bool.TryParse(s, out bool b))
                        {
                            result = (T)(object)b;
                            return true;
                        }
                        if (s == "1") { result = (T)(object)true; return true; }
                        if (s == "0") { result = (T)(object)false; return true; }
                    }
                }

                result = (T)Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        public T GetStat<T>(string key, T defaultValue = default)
        {
            if (TryGetStat<T>(key, out T result))
            {
                return result;
            }
            return defaultValue;
        }

        public T Get<T>(string columnName)
        {
            if (TryGetStat<T>(columnName, out T result))
            {
                return result;
            }

            Debug.LogWarning($"[RowEntity] '{columnName}' 컬럼을 찾을 수 없습니다.");
            return default;
        }

        public IEnumerable<string> GetColumns() => _data.Keys;

        public string GetColumnType(string columnName) => _columnTypes.GetValueOrDefault(columnName, "");

        public bool ContainsColumn(string columnName) => _data.ContainsKey(columnName);
    }
}
