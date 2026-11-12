using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyCSVSystem
{
    /// <summary>
    /// CSV 하나 = 행 목록인 ScriptableObject. TRow는 [Serializable] 클래스/구조체이며, CsvRowMapper 규칙으로 채워집니다.
    /// 사용: <c>[CreateAssetMenu] public class FacilityTable : CsvTableAsset&lt;FacilityRow&gt; { protected override string KeyOf(FacilityRow r) =&gt; r.key; }</c>
    /// </summary>
    public abstract class CsvTableAsset<TRow> : CsvImportedAsset where TRow : new()
    {
        [Tooltip("값을 읽어 올 CSV")]
        [SerializeField] private TextAsset source;

        [SerializeField] private List<TRow> rows = new List<TRow>();

        [NonSerialized] private Dictionary<string, TRow> _index;

        public TextAsset Source => source;

        /// <summary>CSV 행 (OrderColumn이 있으면 그 순서, 없으면 파일 순서)</summary>
        public IReadOnlyList<TRow> Rows => rows;

        public override IEnumerable<TextAsset> Sources
        {
            get { if (source != null) yield return source; }
        }

        /// <summary>정렬 기준 열 (정수). null이면 파일 순서.</summary>
        protected virtual string OrderColumn => null;

        /// <summary>TryGet에 쓰는 행 키. null을 돌려주면 키 조회를 쓰지 않습니다.</summary>
        protected virtual string KeyOf(TRow row) => null;

        /// <summary>행을 읽은 뒤 표 전체를 검사합니다. 문제는 context.Error로 남깁니다.</summary>
        protected virtual void Validate(IReadOnlyList<TRow> importedRows, CsvImportContext context)
        {
        }

        protected sealed override void OnImport(CsvImportContext context)
        {
            rows = context.MapRows<TRow>(source, OrderColumn);
            _index = null; // Validate가 TryGet을 쓸 수 있으므로 새 행 기준으로 다시 만든다
            Validate(rows, context);
        }

        protected override void OnDataChanged() => _index = null;

        /// <summary>KeyOf로 행을 찾습니다 (대소문자 무시).</summary>
        public bool TryGet(string key, out TRow row)
        {
            if (string.IsNullOrEmpty(key))
            {
                row = default;
                return false;
            }

            if (_index == null)
            {
                _index = new Dictionary<string, TRow>(StringComparer.OrdinalIgnoreCase);
                foreach (TRow item in rows)
                {
                    string itemKey = KeyOf(item);
                    if (!string.IsNullOrEmpty(itemKey)) _index[itemKey] = item;
                }
            }

            return _index.TryGetValue(key.Trim(), out row);
        }
    }
}
