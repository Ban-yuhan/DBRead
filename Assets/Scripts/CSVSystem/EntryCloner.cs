using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyCSVSystem
{
    public class EntryCloner : MonoBehaviour
    {
        [Header("연결할 CSV 설정")]
        [Tooltip("읽어올 테이블 이름")]
        public string targetTable = "Facility";

        [Tooltip("가져올 행의 ID")]
        public string targetID = "1";

        [Tooltip("게임 시작 시 자동으로 테이터를 불러올 지 여부")]
        public bool autoInit = true;

        private Dictionary<string, object> _runtimeStats = new Dictionary<string, object>();

        private void Start()
        {
            if(autoInit && !string.IsNullOrEmpty(targetID))
            {
                InitFromDB(targetTable, targetID);
            }
        }

        public void InitFromDB(string tableName, string id)
        {
            if(DatabaseManager.Instance == null)
            {
                Debug.LogError("[EntryCloner] Databasemanager가 씬에 없습니다");
                return;
            }

            var table = DatabaseManager.Instance.GetTable(tableName);
            var row = table?.GetRow(id);

            if(row == null)
            {
                Debug.LogWarning($"[EntryCloner] '{tableName}' 테이블에서 ID '{id}'데이터를 찾을 수 없습니다.");
                return;
            }

            _runtimeStats.Clear();

            foreach(var col in row.GetColumns())
            {
                _runtimeStats[col] = row.Get<object>(col);
            }

            Debug.Log($"<color=cyan>[EntryCloner]</color> {gameObject.name}에 [{tableName} - ID:{id}] 데이터 장착 완료");
        }

        public T GetStat<T>(string key)
        {
            if (_runtimeStats.TryGetValue(key, out object val))
            {
                try { return (T)Convert.ChangeType(val, typeof(T)); }
                catch { return (T)val; }
            }

            Debug.LogWarning($"[EntryCloner] '{key}' 스탯을 찾을 수 없습니다");
            return default;
        }

        public void SetStat(string key, object val)
        {
            if(_runtimeStats.ContainsKey(key))
            {
                _runtimeStats[key] = val;
            }
        }
    }
}
