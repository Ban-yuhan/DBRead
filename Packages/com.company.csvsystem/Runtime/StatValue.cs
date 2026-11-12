using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace MyCSVSystem
{
    /// <summary>
    /// 컬럼 이름 조회와 값 변환 규칙. RowEntity, EntryCloner, StatBinder, CsvRowMapper가 같은 규칙을 씁니다.
    /// </summary>
    public static class StatValue
    {
        /// <summary>목록 컬럼의 항목 구분자. 예: Region0UnLock|NewStage</summary>
        public const char ListSeparator = '|';

        /// <summary>
        /// 정확한 이름으로 찾고, 없으면 언더스코어·대소문자 차이를 무시하고 찾습니다. (ProcessTime == process_time)
        /// </summary>
        public static bool TryFind(Dictionary<string, object> stats, string key, out object value)
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

        public static bool TryConvert<T>(object value, out T result)
        {
            if (value is T direct)
            {
                result = direct;
                return true;
            }

            if (TryConvert(value, typeof(T), out object converted))
            {
                result = (T)converted;
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// 값을 targetType으로 바꿉니다. 지원: 기본형, string, enum(이름·대소문자 무시, 정의된 값만),
        /// T[] / List&lt;T&gt; (문자열을 '|'로 나눠 항목마다 변환, 빈 문자열은 빈 목록).
        /// </summary>
        public static bool TryConvert(object value, Type targetType, out object result)
        {
            result = null;
            if (value == null || targetType == null) return false;

            if (TryGetListElementType(targetType, out Type elementType))
                return TryConvertList(value, targetType, elementType, out result);

            Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (type.IsInstanceOfType(value))
            {
                result = value;
                return true;
            }

            try
            {
                if (type.IsEnum)
                {
                    object parsed = value is string name
                        ? Enum.Parse(type, name.Trim(), true)
                        : Enum.ToObject(type, value);
                    if (!Enum.IsDefined(type, parsed)) return false;
                    result = parsed;
                    return true;
                }

                if (type == typeof(string))
                {
                    result = Convert.ToString(value, CultureInfo.InvariantCulture);
                    return true;
                }

                if (value is string text)
                {
                    text = text.Trim();
                    if (text.Length == 0) return false;
                    if (type == typeof(float) || type == typeof(double)) text = text.TrimEnd('f', 'F', 'd', 'D');
                    if (type == typeof(bool))
                    {
                        if (text == "1") { result = true; return true; }
                        if (text == "0") { result = false; return true; }
                    }
                    value = text;
                }

                result = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception e) when (e is FormatException || e is InvalidCastException || e is OverflowException || e is ArgumentException)
            {
                result = null;
                return false;
            }
        }

        /// <summary>T[] 또는 List&lt;T&gt;이면 T를 돌려줍니다. string은 목록으로 보지 않습니다.</summary>
        public static bool TryGetListElementType(Type type, out Type elementType)
        {
            elementType = null;
            if (type == null || type == typeof(string)) return false;
            if (type.IsArray && type.GetArrayRank() == 1)
            {
                elementType = type.GetElementType();
                return true;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
            return false;
        }

        private static bool TryConvertList(object value, Type listType, Type elementType, out object result)
        {
            result = null;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            string[] parts = string.IsNullOrWhiteSpace(text)
                ? Array.Empty<string>()
                : text.Split(new[] { ListSeparator }, StringSplitOptions.RemoveEmptyEntries);

            var items = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            foreach (string part in parts)
            {
                string item = part.Trim();
                if (item.Length == 0) continue;
                if (!TryConvert(item, elementType, out object converted)) return false;
                items.Add(converted);
            }

            if (listType.IsArray)
            {
                Array array = Array.CreateInstance(elementType, items.Count);
                items.CopyTo(array, 0);
                result = array;
            }
            else
            {
                result = items;
            }
            return true;
        }
    }
}
