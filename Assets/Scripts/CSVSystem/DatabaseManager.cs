using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyCSVSystem
{
    public class DatabaseManager : MonoBehaviour
    {
        //싱글톤 선언
        public static DatabaseManager Instance { get; private set; }

        private Dictionary<string, TableData> _tables = new Dictionary<string, TableData>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        //해당 위치에 있는 이름의 CSV를 읽으라고 명령
        public void LoadTable(string tableName, string resourcePath)
        {
            TextAsset csvFile = Resources.Load<TextAsset>(resourcePath);
            if(csvFile == null)
            {
                Debug.LogError($"[DatabaseManager] CSV 파일을 찾을 수 없습니다: Resources/{resourcePath}");
                return;
            }

            string[] lines = csvFile.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            if(lines.Length <3)
            {
                Debug.LogWarning($"[Databasemanager] '{tableName}' 파일의 데이터가 부족합니다.");
                return;
            }

            string[] headers = lines[0].Trim().Split(',');
            string[] types = lines[1].Trim().Split(',');
            TableData newTable = new TableData();

            for(int i = 2; i < lines.Length; i++)
            {
                string[] values = lines[i].Trim().Split(',');
                if (values.Length < headers.Length) continue;

                RowEntity row = new RowEntity();

                for(int j = 0; j < headers.Length; j++)
                {
                    object parsedValue = CSVParser.ParseValue(types[j], values[j]);
                    
                    row.SetField(headers[j], parsedValue, types[j]);
                }

                newTable.AddRow(values[0], row);
            }

            _tables[tableName] = newTable;
            Debug.Log($"<color=green>[DatabaseManager]</color> 테이블 로드 완료 : <b>{tableName}</b> ({lines.Length - 2}개 행)");
        }

        public TableData GetTable(string tableName)
        {
            if (_tables.TryGetValue(tableName, out var table)) return table;
            //out : 두개 이상의 결과물을 리턴 시키고 싶을 떄, 빈 공간을 만들어서 담아 리턴 시킴.
            //var : 타입 자동맞춤. 변수를 보고 타입을 자동으로 맞춰줌.

            Debug.LogWarning($"[DatabaseManager] '{tableName}' 테이블을 찾을 수 없습니다.");
            return null;
        }

        public void Clear() => _tables.Clear();
    }
}
