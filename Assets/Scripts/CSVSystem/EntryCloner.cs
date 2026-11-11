using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyCSVSystem
{
    /// <summary>
    /// CSV의 한 행을 오브젝트에 연결하고 레벨에 따라 행을 교체합니다.
    /// 레벨은 메모리에만 유지되며, 저장이 필요하면 게임 쪽에서 CurrentLevel을 저장하고 SetLevel로 복원합니다.
    /// </summary>
    public class EntryCloner : MonoBehaviour
    {
        [Header("연동할 CSV 설정")]
        [Tooltip("읽어올 테이블 이름")]
        public string targetTable = "Facility";

        [Tooltip("rowKey가 있으면 시작 레벨, 없으면 연동할 행 ID")]
        public string targetID = "1";

        [Tooltip("복합키 접두어. 예: Stage_1_BurgerMachine_1 -> Stage_1_BurgerMachine_1_{레벨}. 비우면 레벨 번호를 행 ID로 사용")]
        public string rowKey = "";

        [Tooltip("시작 시 자동으로 데이터를 불러올지 여부")]
        public bool autoInit = true;

        public int CurrentLevel { get; private set; } = 1;
        public bool IsInitialized { get; private set; }

        private readonly Dictionary<string, object> _runtimeStats = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private void Start()
        {
            if (autoInit) EnsureInitialized();
        }

        private string ResolveRowId(int level) =>
            string.IsNullOrEmpty(rowKey) ? level.ToString() : $"{rowKey}_{level}";

        public void EnsureInitialized()
        {
            if (IsInitialized) return;

            if (string.IsNullOrEmpty(rowKey))
            {
                if (!string.IsNullOrEmpty(targetID)) InitFromDB(targetTable, targetID);
                return;
            }

            if (!int.TryParse(targetID, out int level)) level = 1;
            InitFromDB(targetTable, ResolveRowId(level));
        }

        /// <summary>
        /// 다음 레벨의 행이 CSV에 있을 때만 레벨을 올립니다. 현재 레벨을 반환합니다.
        /// </summary>
        public int UpgradeLevel()
        {
            EnsureInitialized();
            SetLevel(CurrentLevel + 1);
            return CurrentLevel;
        }

        /// <summary>
        /// 지정한 레벨의 행을 적용합니다. 행이 없으면 현재 상태를 유지하고 false를 반환합니다.
        /// </summary>
        public bool SetLevel(int level)
        {
            string rowId = ResolveRowId(level);
            var table = DatabaseManager.Instance != null ? DatabaseManager.Instance.GetTable(targetTable) : null;
            if (table == null || !table.HasRow(rowId)) return false;

            InitFromDB(targetTable, rowId);
            CurrentLevel = level;
            return true;
        }

        public void InitFromDB(string tableName, string id)
        {
            if (DatabaseManager.Instance == null)
            {
                Debug.LogError("[EntryCloner] DatabaseManager 인스턴스를 찾을 수 없습니다.", this);
                return;
            }

            IsInitialized = true;

            if (!string.IsNullOrEmpty(tableName)) targetTable = tableName;
            if (string.IsNullOrEmpty(rowKey) && !string.IsNullOrEmpty(id)) targetID = id;

            var table = DatabaseManager.Instance.GetTable(tableName);
            var row = table?.GetRow(id);

            if (row == null)
            {
                Debug.LogWarning($"[EntryCloner] '{tableName}' 테이블에서 ID '{id}' 데이터를 찾을 수 없습니다. ({gameObject.name})", this);
                return;
            }

            _runtimeStats.Clear();
            foreach (var col in row.GetColumns())
            {
                _runtimeStats[col] = row.Get<object>(col);
            }

            if (row.TryGetStat("level", out int rowLevel)) CurrentLevel = rowLevel;
        }

        public bool TryGetStat<T>(string key, out T result)
        {
            EnsureInitialized();

            if (RowEntity.TryFind(_runtimeStats, key, out object value))
            {
                return RowEntity.TryConvert(value, out result);
            }

            result = default;
            return false;
        }

        public T GetStat<T>(string key, T defaultValue = default)
        {
            EnsureInitialized();

            if (!RowEntity.TryFind(_runtimeStats, key, out object value))
            {
                Debug.LogWarning($"[EntryCloner] '{targetTable}' 테이블에서 '{key}' 컬럼을 찾을 수 없어 기본값 '{defaultValue}'을(를) 사용합니다. ({gameObject.name})", this);
                return defaultValue;
            }

            return RowEntity.TryConvert(value, out T result) ? result : defaultValue;
        }

        public void SetStat(string key, object val)
        {
            _runtimeStats[key] = val;
        }
    }
}
