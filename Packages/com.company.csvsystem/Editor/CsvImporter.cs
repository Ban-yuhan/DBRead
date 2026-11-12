using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MyCSVSystem.Editor
{
    /// <summary>
    /// CsvImportedAsset을 CSV에서 다시 채웁니다.
    /// - 메뉴 Tools/CSV/Reimport All: 프로젝트의 모든 CsvImportedAsset
    /// - CSV 저장 시 자동: 그 CSV를 Sources로 가진 에셋만 (CsvPostprocessor)
    /// - 에셋 인스펙터의 버튼: 그 에셋만
    /// </summary>
    public static class CsvImporter
    {
        private const string LogTag = "<b>[CSV Import]</b>";

        [MenuItem("Tools/CSV/Reimport All")]
        public static void ReimportAllMenu() => ReimportAll();

        /// <summary>모든 CsvImportedAsset을 다시 임포트합니다. 오류가 하나라도 있으면 false.</summary>
        public static bool ReimportAll()
        {
            List<CsvImportedAsset> assets = FindAllAssets();
            bool ok = Import(assets);
            Debug.Log($"{LogTag} {assets.Count}개 에셋 임포트 {(ok ? "완료" : "— 오류 있음, 위 로그 확인")}");
            return ok;
        }

        /// <summary>CSV 경로들을 Sources로 가진 에셋만 다시 임포트합니다.</summary>
        public static void ReimportUsing(ICollection<string> csvPaths)
        {
            if (csvPaths == null || csvPaths.Count == 0) return;

            var targets = FindAllAssets()
                .Where(asset => asset.Sources.Any(source => source != null && csvPaths.Contains(AssetDatabase.GetAssetPath(source))))
                .ToList();
            if (targets.Count > 0) Import(targets);
        }

        public static bool Import(IEnumerable<CsvImportedAsset> assets)
        {
            bool ok = true;
            foreach (CsvImportedAsset asset in assets)
                ok &= ImportOne(asset);
            AssetDatabase.SaveAssets();
            return ok;
        }

        /// <summary>
        /// 에셋 하나를 임포트하고 결과를 콘솔에 남깁니다. 저장(SaveAssets)은 호출하는 쪽이 합니다.
        /// - 오류가 있으면 데이터는 임포트 전 상태로 되돌리고 오류만 기록합니다 (반쯤 깨진 데이터가 저장되지 않게).
        /// - 결과가 이전과 같으면 에셋을 건드리지 않습니다 (VCS 변경이 생기지 않게).
        /// </summary>
        public static bool ImportOne(CsvImportedAsset asset)
        {
            if (asset == null) return false;

            string before = EditorJsonUtility.ToJson(asset);
            var context = new CsvImportContext(ResolveAssets);
            try
            {
                asset.Import(context);
            }
            catch (Exception e)
            {
                EditorJsonUtility.FromJsonOverwrite(before, asset);
                asset.MarkFailedImport(new[] { $"임포트 중 예외: {e.Message}" });
                EditorUtility.SetDirty(asset);
                Debug.LogException(e, asset);
                return false;
            }

            bool failed = asset.HasImportErrors;
            if (failed)
            {
                var errors = asset.ImportErrors.ToList();
                EditorJsonUtility.FromJsonOverwrite(before, asset);
                asset.MarkFailedImport(errors);
            }

            bool changed = EditorJsonUtility.ToJson(asset) != before;
            if (changed)
            {
                if (!failed) asset.StampImportTime(DateTime.Now);
                EditorUtility.SetDirty(asset);
            }

            string sources = string.Join(", ", asset.Sources.Where(source => source != null).Select(source => source.name));
            if (!failed)
            {
                Debug.Log($"{LogTag} {asset.name} ← {sources}{(changed ? "" : " (변경 없음)")}", asset);
                return true;
            }

            Debug.LogError($"{LogTag} {asset.name}: 오류 {asset.ImportErrors.Count}개 — 이전 데이터를 유지합니다. ({sources})", asset);
            foreach (string error in asset.ImportErrors)
                Debug.LogError($"{LogTag} {asset.name}: {error}", asset);
            return false;
        }

        /// <summary>마지막 임포트 이후 CSV가 바뀐 에셋을 다시 임포트합니다. 다시 임포트한 개수를 돌려줍니다.</summary>
        public static int ReimportStale(string reason)
        {
            List<CsvImportedAsset> stale = FindAllAssets().Where(asset => asset.IsStale).ToList();
            if (stale.Count == 0) return 0;

            Debug.Log($"{LogTag} {reason}: CSV와 어긋난 에셋 {stale.Count}개를 다시 임포트합니다 ({string.Join(", ", stale.Select(asset => asset.name))}).");
            Import(stale);
            return stale.Count;
        }

        public static List<CsvImportedAsset> FindAllAssets()
        {
            return AssetDatabase.FindAssets("t:" + nameof(CsvImportedAsset))
                .Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<CsvImportedAsset>()
                .Distinct()
                .ToList();
        }

        /// <summary>이름과 타입이 맞는 에셋 후보 (스프라이트 같은 하위 에셋 포함)</summary>
        private static Object[] ResolveAssets(string assetName, Type type)
        {
            // 검색어는 공백으로 나뉘어 부분 일치하므로 넓게 찾고 이름이 정확히 같은 것만 남긴다
            return AssetDatabase.FindAssets($"t:{type.Name} {assetName}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .Where(asset => asset != null && type.IsInstanceOfType(asset) && asset.name == assetName)
                .ToArray();
        }
    }

    /// <summary>CSV가 추가·수정되면 그 CSV를 쓰는 에셋을 자동으로 다시 임포트합니다.</summary>
    internal sealed class CsvPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> Pending = new HashSet<string>();

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool added = false;
            foreach (string path in importedAssets.Concat(movedAssets))
            {
                if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    added |= Pending.Add(path);
            }
            if (!added) return;

            // 임포트 도중에는 에셋을 저장하지 않는다. 에셋 임포트·컴파일이 끝난 뒤 처리한다.
            // (delayCall은 에디터가 백그라운드일 때 밀릴 수 있어 update로 기다린다)
            EditorApplication.update -= Flush;
            EditorApplication.update += Flush;
        }

        private static void Flush()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= Flush;

            var paths = new List<string>(Pending);
            Pending.Clear();
            CsvImporter.ReimportUsing(paths);
        }
    }

    /// <summary>
    /// 자동 재임포트를 놓친 경우의 안전망. 에디터를 켜거나 스크립트를 다시 불러온 직후, 그리고 Play에 들어가기 직전에
    /// CSV 해시와 에셋에 기록된 해시를 비교해 어긋난 에셋을 다시 임포트합니다.
    /// (Postprocessor의 대기 목록은 도메인 리로드 때 사라지고, 에디터가 꺼진 동안 바뀐 CSV는 감지하지 못하므로)
    /// </summary>
    [InitializeOnLoad]
    internal static class CsvStaleChecker
    {
        static CsvStaleChecker()
        {
            EditorApplication.update += CheckWhenIdle;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    CsvImporter.ReimportStale("Play 시작 전");
            };
        }

        /// <summary>도메인 리로드 직후에는 컴파일·에셋 임포트가 끝나지 않았을 수 있어, 한가해질 때 한 번만 검사한다</summary>
        private static void CheckWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= CheckWhenIdle;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                CsvImporter.ReimportStale("에디터 로드");
        }
    }
}
