using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MyCSVSystem
{
    /// <summary>
    /// CsvImportedAsset.Import 한 번에 쓰는 도구 모음. CSV 읽기, 행 매핑, 에셋 찾기, 오류 수집을 맡습니다.
    /// 에디터 임포터(CsvImporter)가 만들어 넘깁니다.
    /// </summary>
    public sealed class CsvImportContext
    {
        private readonly List<string> _errors = new List<string>();
        private readonly Dictionary<TextAsset, TableData> _cache = new Dictionary<TextAsset, TableData>();
        private readonly Func<string, Type, Object[]> _assetResolver;

        /// <param name="assetResolver">이름과 타입으로 프로젝트 에셋을 찾는 함수 (에디터에서 AssetDatabase로 구현). null이면 FindAsset이 항상 실패합니다.</param>
        public CsvImportContext(Func<string, Type, Object[]> assetResolver = null)
        {
            _assetResolver = assetResolver;
        }

        public IReadOnlyList<string> Errors => _errors;
        public bool HasErrors => _errors.Count > 0;

        public void Error(string message) => _errors.Add(message);

        /// <summary>CSV를 읽습니다. 같은 파일은 한 번만 파싱합니다. 실패하면 오류를 남기고 null.</summary>
        public TableData Read(TextAsset csv)
        {
            if (csv == null)
            {
                Error("CSV 파일이 연결되지 않았습니다.");
                return null;
            }

            if (_cache.TryGetValue(csv, out TableData cached)) return cached;

            TableData table = CsvTableReader.Read(csv.text, csv.name, _errors);
            _cache[csv] = table;
            return table;
        }

        /// <summary>table의 첫 행에 columns가 모두 있는지 검사합니다.</summary>
        public bool RequireColumns(TableData table, string tableName, IEnumerable<string> columns)
        {
            if (table == null) return false;
            RowEntity first = table.AllRows.FirstOrDefault();
            if (first == null)
            {
                Error($"{tableName}: 데이터 행이 없습니다.");
                return false;
            }

            bool ok = true;
            foreach (string column in columns)
            {
                if (!first.TryGetRaw(column, out _))
                {
                    Error($"{tableName}: '{column}' 열이 없습니다.");
                    ok = false;
                }
            }
            return ok;
        }

        /// <summary>
        /// CSV의 모든 행을 T로 옮깁니다. orderColumn이 있으면 그 열(정수)로 정렬하고, 없으면 파일 순서.
        /// </summary>
        public List<T> MapRows<T>(TextAsset csv, string orderColumn = null) where T : new()
        {
            var result = new List<T>();
            TableData table = Read(csv);
            if (table == null) return result;

            if (table.Count > 0)
            {
                // 열이 통째로 없으면 행마다 같은 오류가 나오므로 먼저 한 번만 검사한다
                if (!RequireColumns(table, csv.name, CsvRowMapper.RequiredColumns(typeof(T)))) return result;
                if (orderColumn != null && !RequireColumns(table, csv.name, new[] { orderColumn })) return result;
            }

            IEnumerable<KeyValuePair<string, RowEntity>> rows = table.RowIds.Select(id => new KeyValuePair<string, RowEntity>(id, table.GetRow(id)));
            if (orderColumn != null)
                rows = rows.OrderBy(pair => pair.Value.GetStat<int>(orderColumn));

            foreach (var pair in rows)
                result.Add(CsvRowMapper.Map<T>(pair.Value, $"{csv.name} [{pair.Key}]", _errors));

            return result;
        }

        /// <summary>이름이 정확히 같은 에셋을 하나 찾습니다. 없거나 여러 개면 오류를 남기고 null.</summary>
        public T FindAsset<T>(string assetName, string label) where T : Object
        {
            if (string.IsNullOrEmpty(assetName)) return null;

            Object[] found = _assetResolver != null ? _assetResolver(assetName, typeof(T)) : Array.Empty<Object>();
            T[] matches = found.OfType<T>().Where(asset => asset.name == assetName).Distinct().ToArray();

            if (matches.Length == 1) return matches[0];

            Error(matches.Length == 0
                ? $"{label}: {typeof(T).Name} '{assetName}'을(를) 프로젝트에서 찾지 못했습니다."
                : $"{label}: {typeof(T).Name} '{assetName}'이(가) {matches.Length}개 있어 하나로 정할 수 없습니다.");
            return null;
        }
    }
}
