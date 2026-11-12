using System;

namespace MyCSVSystem
{
    /// <summary>필드에 연결할 CSV 컬럼 이름을 직접 지정합니다. 없으면 필드 이름으로 찾습니다 (processTime == process_time).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CsvColumnAttribute : Attribute
    {
        public string Name { get; }
        public CsvColumnAttribute(string name) => Name = name;
    }

    /// <summary>CSV에서 읽지 않는 필드. 임포트 후 코드에서 채우는 값에 붙입니다.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CsvIgnoreAttribute : Attribute
    {
    }

    /// <summary>컬럼이 없거나 칸이 비어 있어도 오류로 보지 않고 필드의 기본값을 유지합니다.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CsvOptionalAttribute : Attribute
    {
    }
}
