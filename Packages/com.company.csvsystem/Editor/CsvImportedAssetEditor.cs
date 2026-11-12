using UnityEditor;
using UnityEngine;

namespace MyCSVSystem.Editor
{
    /// <summary>CsvImportedAsset 인스펙터: 다시 임포트 버튼과 마지막 임포트 결과를 보여 줍니다.</summary>
    [CustomEditor(typeof(CsvImportedAsset), true)]
    public class CsvImportedAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (CsvImportedAsset)target;

            if (GUILayout.Button("CSV에서 다시 가져오기"))
            {
                CsvImporter.ImportOne(asset);
                AssetDatabase.SaveAssets();
            }

            if (asset.HasImportErrors)
                EditorGUILayout.HelpBox($"CSV에 오류 {asset.ImportErrors.Count}개 — 아래 데이터는 마지막 정상 임포트({(string.IsNullOrEmpty(asset.ImportedAt) ? "없음" : asset.ImportedAt)}) 값입니다.\n- " + string.Join("\n- ", asset.ImportErrors), MessageType.Error);
            else if (string.IsNullOrEmpty(asset.ImportedAt))
                EditorGUILayout.HelpBox("아직 임포트하지 않았습니다. CSV를 연결하고 위 버튼을 누르세요.", MessageType.Warning);
            else
                EditorGUILayout.HelpBox($"마지막 데이터 변경 {asset.ImportedAt}: 정상", MessageType.Info);

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
