using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MyCSVSystem
{
    /// <summary>
    /// RowEntity 한 행을 [Serializable] 클래스/구조체로 옮깁니다.
    /// 대상 필드: public 필드와 [SerializeField] 필드 (NonSerialized, CsvIgnore 제외).
    /// 컬럼 이름: [CsvColumn]이 있으면 그 이름, 없으면 필드 이름 (언더스코어·대소문자 무시).
    /// 규칙:
    /// - 컬럼이 없으면 오류 ([CsvOptional]이면 기본값 유지)
    /// - 문자열 칸이 비었는데 대상이 enum·숫자 등이면 오류 ([CsvOptional]이면 기본값 유지). 목록은 빈 목록
    /// - 숫자 타입 컬럼의 빈칸은 파서가 이미 0으로 읽는다 (패키지 공통 규칙)
    /// </summary>
    public static class CsvRowMapper
    {
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static T Map<T>(RowEntity row, string rowLabel, List<string> errors) where T : new()
        {
            return (T)Map(typeof(T), row, rowLabel, errors);
        }

        public static object Map(Type type, RowEntity row, string rowLabel, List<string> errors)
        {
            // 구조체는 박싱된 인스턴스에 써야 값이 남는다
            object instance = Activator.CreateInstance(type);

            foreach (FieldInfo field in MappedFields(type))
            {
                string column = ColumnName(field);
                bool optional = field.IsDefined(typeof(CsvOptionalAttribute), true);

                if (!row.TryGetRaw(column, out object raw))
                {
                    if (!optional) errors?.Add($"{rowLabel}: '{column}' 열이 없습니다 ({type.Name}.{field.Name}).");
                    continue;
                }

                bool isList = StatValue.TryGetListElementType(field.FieldType, out _);
                if (!isList && field.FieldType != typeof(string) && raw is string text && string.IsNullOrWhiteSpace(text))
                {
                    if (!optional) errors?.Add($"{rowLabel}: '{column}' 칸이 비어 있습니다.");
                    continue;
                }

                if (!StatValue.TryConvert(raw, field.FieldType, out object value))
                {
                    errors?.Add($"{rowLabel}: '{column}' 값 '{raw}'을(를) {FriendlyName(field.FieldType)}(으)로 읽을 수 없습니다.");
                    continue;
                }

                field.SetValue(instance, value);
            }

            return instance;
        }

        /// <summary>type이 읽는 컬럼 이름 목록 (필수 열 검사용)</summary>
        public static IEnumerable<string> RequiredColumns(Type type)
        {
            foreach (FieldInfo field in MappedFields(type))
            {
                if (!field.IsDefined(typeof(CsvOptionalAttribute), true))
                    yield return ColumnName(field);
            }
        }

        private static IEnumerable<FieldInfo> MappedFields(Type type)
        {
            foreach (FieldInfo field in type.GetFields(FieldFlags))
            {
                if (field.IsInitOnly || field.IsLiteral) continue;
                if (field.IsNotSerialized || field.IsDefined(typeof(CsvIgnoreAttribute), true)) continue;
                if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), true)) continue;
                yield return field;
            }
        }

        private static string ColumnName(FieldInfo field)
        {
            var attribute = field.GetCustomAttribute<CsvColumnAttribute>(true);
            return attribute != null && !string.IsNullOrEmpty(attribute.Name) ? attribute.Name : field.Name;
        }

        private static string FriendlyName(Type type)
        {
            if (StatValue.TryGetListElementType(type, out Type element)) return $"{element.Name} 목록";
            return type.Name;
        }
    }
}
