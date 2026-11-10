using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MyCSVSystem
{
    public class EntryCloner : MonoBehaviour
    {
        [Header("연동할 CSV 설정")]
        [Tooltip("읽어올 테이블 이름")]
        public string targetTable = "Facility";

        [Tooltip("연동할 데이터 ID")]
        public string targetID = "1";

        [Tooltip("시작 시 자동으로 데이터를 불러올지 여부")]
        public bool autoInit = true;

        [Header("세이브 데이터 연동")]
        public bool useSaveData = true;

        [Tooltip("비워둘 경우 gameObject.name을 자동으로 사용")]
        public string saveKey = "";

        public string EffectiveSaveKey => string.IsNullOrEmpty(saveKey) ? gameObject.name : saveKey;

        private Dictionary<string, object> _runtimeStats = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private bool _isInitialized = false;

        private void Start()
        {
            if (autoInit && !_isInitialized)
            {
                EnsureInitialized();
            }
        }

        /// <summary>
        /// 레벨에 해당하는 적절한 행 ID(복합키 또는 단순 레벨 번호)를 스마트하게 판별합니다.
        /// </summary>
        private string ResolveRowIdForLevel(int level)
        {
            if (DatabaseManager.Instance != null)
            {
                var table = DatabaseManager.Instance.GetTable(targetTable);
                if (table != null)
                {
                    // 1. {EffectiveSaveKey}_{level} 복합키 존재 확인 (예: BurgerMachine_1_1, Stage_1_BurgerMachine_1_1)
                    string compositeKey = $"{EffectiveSaveKey}_{level}";
                    if (table.HasRow(compositeKey))
                    {
                        return compositeKey;
                    }

                    // 2. Stage_1_ 접두어가 누락된 경우 자동 보정 (예: saveKey="BurgerMachine_1" -> "Stage_1_BurgerMachine_1_1")
                    if (!EffectiveSaveKey.StartsWith("Stage_1_", StringComparison.OrdinalIgnoreCase))
                    {
                        string stageCompositeKey = $"Stage_1_{EffectiveSaveKey}_{level}";
                        if (table.HasRow(stageCompositeKey))
                        {
                            return stageCompositeKey;
                        }
                    }
                }
            }

            // 3. 복합키가 없으면 기존 단순 레벨 번호 (예: "1", "2") 반환 (100% 하위 호환)
            return level.ToString();
        }

        private void EnsureInitialized()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            if (useSaveData && SaveManager.Instance != null)
            {
                int level = SaveManager.Instance.GetLevel(EffectiveSaveKey, 1);
                string rowId = ResolveRowIdForLevel(level);
                InitFromDB(targetTable, rowId);
            }
            else if (!string.IsNullOrEmpty(targetID))
            {
                InitFromDB(targetTable, targetID);
            }
        }

        public int UpgradeLevel(int maxLevel = 10)
        {
            if (SaveManager.Instance != null)
            {
                int newLevel = SaveManager.Instance.UpgradeLevel(EffectiveSaveKey, maxLevel);
                string rowId = ResolveRowIdForLevel(newLevel);
                InitFromDB(targetTable, rowId);
                return newLevel;
            }
            else
            {
                Debug.LogWarning($"[EntryCloner] SaveManager 인스턴스를 찾을 수 없습니다. ({gameObject.name})");
                if (int.TryParse(targetID, out int cur)) return cur;
                return 1;
            }
        }

        public void InitFromDB(string tableName, string id)
        {
            _isInitialized = true;

            if (!string.IsNullOrEmpty(tableName))
            {
                targetTable = tableName;
            }
            if (!string.IsNullOrEmpty(id))
            {
                targetID = id;
            }

            if (DatabaseManager.Instance == null)
            {
                Debug.LogError("[EntryCloner] DatabaseManager 인스턴스를 찾을 수 없습니다.");
                return;
            }

            var table = DatabaseManager.Instance.GetTable(tableName);
            var row = table?.GetRow(id);

            if (row == null)
            {
                Debug.LogWarning($"[EntryCloner] '{tableName}' 테이블에서 ID '{id}' 데이터를 찾을 수 없습니다.");
                return;
            }

            _runtimeStats.Clear();

            foreach (var col in row.GetColumns())
            {
                _runtimeStats[col] = row.Get<object>(col);
            }

            Debug.Log($"<color=cyan>[EntryCloner]</color> {gameObject.name}에 [{tableName} - ID:{id}] 데이터 장착 완료");
        }

        public bool TryGetStat<T>(string key, out T result)
        {
            if (!_isInitialized)
            {
                EnsureInitialized();
            }

            if (string.IsNullOrEmpty(key))
            {
                result = default;
                return false;
            }

            if (!_runtimeStats.TryGetValue(key, out object val) || val == null)
            {
                // Fallback 1: 언더스코어 유무 차이 자동 보정 (예: ProduceTime <-> produce_time, processtime <-> process_time)
                string strippedKey = key.Replace("_", "");
                foreach (var kvp in _runtimeStats)
                {
                    if (kvp.Key.Replace("_", "").Equals(strippedKey, StringComparison.OrdinalIgnoreCase))
                    {
                        val = kvp.Value;
                        break;
                    }
                }

                // Fallback 2: ProduceTime <-> process_time 상호 호환 매핑
                if (val == null)
                {
                    if (key.Equals("ProduceTime", StringComparison.OrdinalIgnoreCase))
                    {
                        _runtimeStats.TryGetValue("process_time", out val);
                    }
                    else if (key.Equals("process_time", StringComparison.OrdinalIgnoreCase))
                    {
                        _runtimeStats.TryGetValue("ProduceTime", out val);
                    }
                }

                if (val == null)
                {
                    result = default;
                    return false;
                }
            }

            if (val is T direct)
            {
                result = direct;
                return true;
            }

            try
            {
                Type targetType = typeof(T);
                Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                if (underlyingType.IsEnum)
                {
                    if (val is string strVal)
                    {
                        result = (T)Enum.Parse(underlyingType, strVal, true);
                        return true;
                    }
                    result = (T)Enum.ToObject(underlyingType, val);
                    return true;
                }

                if (underlyingType == typeof(string))
                {
                    result = (T)(object)val.ToString();
                    return true;
                }

                if (val is string s)
                {
                    s = s.Trim();
                    if (string.IsNullOrEmpty(s))
                    {
                        result = default;
                        return false;
                    }

                    if (underlyingType == typeof(float))
                    {
                        if (float.TryParse(s.TrimEnd('f', 'F'), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float f))
                        {
                            result = (T)(object)f;
                            return true;
                        }
                    }
                    else if (underlyingType == typeof(double))
                    {
                        if (double.TryParse(s.TrimEnd('f', 'F', 'd', 'D'), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double d))
                        {
                            result = (T)(object)d;
                            return true;
                        }
                    }
                    else if (underlyingType == typeof(int))
                    {
                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                        {
                            result = (T)(object)i;
                            return true;
                        }
                    }
                    else if (underlyingType == typeof(long))
                    {
                        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                        {
                            result = (T)(object)l;
                            return true;
                        }
                    }
                    else if (underlyingType == typeof(bool))
                    {
                        if (bool.TryParse(s, out bool b))
                        {
                            result = (T)(object)b;
                            return true;
                        }
                        if (s == "1") { result = (T)(object)true; return true; }
                        if (s == "0") { result = (T)(object)false; return true; }
                    }
                }

                result = (T)Convert.ChangeType(val, underlyingType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        public T GetStat<T>(string key, T defaultValue = default)
        {
            if (TryGetStat<T>(key, out T result))
            {
                return result;
            }

            return defaultValue;
        }

        public void SetStat(string key, object val)
        {
            _runtimeStats[key] = val;
        }
    }
}
