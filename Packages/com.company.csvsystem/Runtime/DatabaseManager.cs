using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyCSVSystem
{
    /// <summary>
    /// Resources 폴더의 CSV를 로드해 테이블로 보관합니다.
    /// 씬에 없으면 처음 접근할 때 자동 생성되며, 다른 스크립트의 Awake보다 먼저 초기화됩니다.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public class DatabaseManager : MonoBehaviour
    {
        private static DatabaseManager _instance;
        private static bool _isQuitting;

        public static DatabaseManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (_isQuitting || !Application.isPlaying) return null;

#if UNITY_2023_1_OR_NEWER
                _instance = FindFirstObjectByType<DatabaseManager>();
#else
                _instance = FindObjectOfType<DatabaseManager>();
#endif
                if (_instance == null)
                {
                    new GameObject("@DatabaseManager").AddComponent<DatabaseManager>();
                }
                return _instance;
            }
        }

        [Tooltip("Resources 아래의 CSV 폴더 경로")]
        [SerializeField] private string resourceFolder = "Data";

        private Dictionary<string, TableData> _tables = new Dictionary<string, TableData>(StringComparer.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _isQuitting = false;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAllTablesInFolder(resourceFolder);
        }

        private void OnApplicationQuit() => _isQuitting = true;

        public void LoadTable(string tableName, string resourcePath)
        {
            TextAsset csvFile = Resources.Load<TextAsset>(resourcePath);
            if (csvFile == null)
            {
                Debug.LogError($"[DatabaseManager] CSV 파일을 찾을 수 없습니다: Resources/{resourcePath}");
                return;
            }

            var errors = new List<string>();
            TableData newTable = CsvTableReader.Read(csvFile.text, tableName, errors);
            foreach (string error in errors)
                Debug.LogWarning($"[DatabaseManager] {error}");

            if (newTable == null || newTable.Count == 0)
            {
                Debug.LogWarning($"[DatabaseManager] '{tableName}'의 데이터가 부족합니다.");
                return;
            }

            _tables[tableName] = newTable;
            Debug.Log($"<color=green>[DatabaseManager]</color> 테이블 로드 완료 : <b>{tableName}</b> ({newTable.Count}개 행)");
        }

        public TableData GetTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return null;

            if (_tables.TryGetValue(tableName, out var table)) return table;

            // 'Facility' <-> 'FacilityData' 상호 호환 자동 fallback
            if (tableName.EndsWith("Data", StringComparison.OrdinalIgnoreCase))
            {
                string stripped = tableName.Substring(0, tableName.Length - 4);
                if (_tables.TryGetValue(stripped, out table)) return table;
            }
            else
            {
                string appended = tableName + "Data";
                if (_tables.TryGetValue(appended, out table)) return table;
            }

            Debug.LogWarning($"[DatabaseManager] '{tableName}' 테이블을 찾을 수 없습니다.");
            return null;
        }

        public void Clear() => _tables.Clear();

        public void LoadAllTablesInFolder(string folderPath)
        {
            TextAsset[] allCsvFiles = Resources.LoadAll<TextAsset>(folderPath);

            if (allCsvFiles == null || allCsvFiles.Length == 0)
            {
                Debug.LogWarning($"[DatabaseManager] '{folderPath}' 내 CSV 파일이 없습니다.");
                return;
            }

            foreach (TextAsset csv in allCsvFiles)
            {
                string tableName = csv.name;
                LoadTable(tableName, $"{folderPath}/{tableName}");
            }

            Debug.Log($"<color=cyan><b>[DatabaseManager]</b> 총 {allCsvFiles.Length}개 CSV 테이블 자동 로드 완료!</color>");
        }
    }
}
