using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyCSVSystem
{
    public class DatabaseManager : MonoBehaviour
    {
        public static DatabaseManager Instance { get; private set; }

        private Dictionary<string, TableData> _tables = new Dictionary<string, TableData>(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                LoadAllTablesInFolder("Data");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void LoadTable(string tableName, string resourcePath)
        {
            TextAsset csvFile = Resources.Load<TextAsset>(resourcePath);
            if (csvFile == null)
            {
                Debug.LogError($"[DatabaseManager] CSV 파일을 찾을 수 없습니다: Resources/{resourcePath}");
                return;
            }

            List<List<string>> rows = CSVParser.ParseCSV(csvFile.text);

            if (rows == null || rows.Count < 3)
            {
                Debug.LogWarning($"[DatabaseManager] '{tableName}'의 데이터가 부족합니다.");
                return;
            }

            List<string> headers = rows[0];
            List<string> types = rows[1];
            TableData newTable = new TableData();

            int loadedRowCount = 0;

            for (int i = 2; i < rows.Count; i++)
            {
                List<string> values = rows[i];

                // 완전히 비어있는 행 건너뛰기
                bool allEmpty = true;
                for (int k = 0; k < values.Count; k++)
                {
                    if (!string.IsNullOrWhiteSpace(values[k]))
                    {
                        allEmpty = false;
                        break;
                    }
                }
                if (allEmpty) continue;

                // 부족한 필드에 string.Empty를 자동 패딩하여 파서로 넘김 (사일런트 행 누락 방지)
                string[] paddedValues = new string[headers.Count];
                for (int j = 0; j < headers.Count; j++)
                {
                    if (j < values.Count)
                    {
                        paddedValues[j] = values[j];
                    }
                    else
                    {
                        paddedValues[j] = string.Empty;
                    }
                }

                RowEntity row = new RowEntity();

                for (int j = 0; j < headers.Count; j++)
                {
                    string header = headers[j].Trim();
                    if (string.IsNullOrEmpty(header)) continue;

                    string colType = j < types.Count ? types[j].Trim() : "string";
                    object parsedValue = CSVParser.ParseValue(colType, paddedValues[j]);

                    row.SetField(header, parsedValue, colType);
                }

                string rowId = paddedValues[0].Trim();
                newTable.AddRow(rowId, row);
                loadedRowCount++;
            }

            _tables[tableName] = newTable;
            Debug.Log($"<color=green>[DatabaseManager]</color> 테이블 로드 완료 : <b>{tableName}</b> ({loadedRowCount}개 행)");
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
