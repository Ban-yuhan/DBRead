using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MyCSVSystem
{
    /// <summary>
    /// CSV에서 값을 받아 저장하는 ScriptableObject의 기반 클래스.
    /// 에디터에서 연결된 CSV가 바뀌면 CsvImporter가 Import를 불러 다시 채웁니다. 런타임에는 CSV를 읽지 않습니다.
    /// 여러 CSV를 합치는 에셋은 이 클래스를, CSV 하나 = 행 목록인 에셋은 CsvTableAsset&lt;TRow&gt;를 상속합니다.
    /// </summary>
    public abstract class CsvImportedAsset : ScriptableObject
    {
        [SerializeField, HideInInspector] private List<string> importErrors = new List<string>();
        [SerializeField, HideInInspector] private string importedAt;
        [SerializeField, HideInInspector] private string sourceHash;

        /// <summary>마지막 임포트에서 나온 오류. 비어 있으면 정상.</summary>
        public IReadOnlyList<string> ImportErrors => importErrors;
        public bool HasImportErrors => importErrors != null && importErrors.Count > 0;
        /// <summary>데이터가 마지막으로 바뀐 임포트 시각 (비어 있으면 한 번도 임포트하지 않음)</summary>
        public string ImportedAt => importedAt;
        /// <summary>마지막 임포트 때 Sources 내용의 해시</summary>
        public string SourceHash => sourceHash;

        /// <summary>이 에셋이 읽는 CSV. 하나라도 바뀌면 다시 임포트합니다.</summary>
        public abstract IEnumerable<TextAsset> Sources { get; }

        /// <summary>
        /// CSV를 읽어 필드를 다시 채웁니다. 오류는 context에 모이고 ImportErrors에 저장됩니다.
        /// 시각 기록과 "오류 시 이전 데이터 유지"는 에디터 임포터(CsvImporter)가 맡습니다.
        /// </summary>
        public void Import(CsvImportContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            OnImport(context);
            importErrors = new List<string>(context.Errors);
            sourceHash = ComputeSourceHash();
            OnDataChanged();
        }

        /// <summary>
        /// 실패한 임포트의 결과만 남깁니다 (데이터는 호출하는 쪽이 이전 상태로 되돌린 뒤 부릅니다).
        /// 해시는 현재 CSV 기준으로 기록해, 고치기 전까지 같은 오류로 계속 다시 임포트하지 않게 합니다.
        /// </summary>
        public void MarkFailedImport(IEnumerable<string> errors)
        {
            importErrors = new List<string>(errors);
            sourceHash = ComputeSourceHash();
            OnDataChanged();
        }

        /// <summary>데이터가 바뀐 성공 임포트의 시각을 기록합니다.</summary>
        public void StampImportTime(DateTime time) => importedAt = time.ToString("yyyy-MM-dd HH:mm:ss");

        /// <summary>현재 Sources(이름 + 내용)의 해시. 에디터에서 CSV와 에셋이 어긋났는지 확인할 때 씁니다.</summary>
        public string ComputeSourceHash()
        {
            var builder = new StringBuilder();
            foreach (TextAsset source in Sources)
            {
                if (source == null) continue;
                builder.Append(source.name).Append(':').Append(Hash128.Compute(source.text).ToString()).Append(';');
            }
            return Hash128.Compute(builder.ToString()).ToString();
        }

        /// <summary>마지막 임포트 이후 CSV가 바뀌었는지 (에디터 전용으로 쓰세요: CSV 전체를 읽습니다)</summary>
        public bool IsStale => string.IsNullOrEmpty(sourceHash) || sourceHash != ComputeSourceHash();

        /// <summary>context로 CSV를 읽어 필드를 채웁니다. 검증 실패는 context.Error로 남깁니다.</summary>
        protected abstract void OnImport(CsvImportContext context);

        /// <summary>임포트 직후와 에셋 로드(OnEnable) 때 불립니다. 조회용 캐시를 비우는 곳.</summary>
        protected virtual void OnDataChanged()
        {
        }

        protected virtual void OnEnable() => OnDataChanged();
    }
}
