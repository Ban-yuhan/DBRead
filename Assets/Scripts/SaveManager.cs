using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MyCSVSystem
{
    [System.Serializable]
    public class LevelEntry
    {
        public string key;
        public int level;

        public LevelEntry() { }

        public LevelEntry(string key, int level)
        {
            this.key = key;
            this.level = level;
        }
    }

    [System.Serializable]
    public class SaveData
    {
        public int gold = 1000;
        public int burgerMachineLevel = 1;
        public List<LevelEntry> facilityLevels = new List<LevelEntry>();
    }

    public class SaveManager : MonoBehaviour
    {
        private static SaveManager _instance;

        public static SaveManager Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    _instance = FindFirstObjectByType<SaveManager>();
#else
                    _instance = FindObjectOfType<SaveManager>();
#endif
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("SaveManager");
                        _instance = go.AddComponent<SaveManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        public SaveData data;

        public int burgerMachineLevel
        {
            get
            {
                if (data == null) Load();
                return data.burgerMachineLevel;
            }
            set
            {
                if (data == null) Load();
                data.burgerMachineLevel = value;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                Load();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void Load()
        {
            string path = Application.persistentDataPath + "/SaveData.json";

            if (!File.Exists(path))
            {
                data = new SaveData();
                Save();
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(path);
                    data = JsonUtility.FromJson<SaveData>(json);
                    if (data == null)
                    {
                        data = new SaveData();
                        Save();
                    }
                    else if (data.facilityLevels == null)
                    {
                        data.facilityLevels = new List<LevelEntry>();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveManager] 세이브 파일 로드 중 오류 발생: {ex.Message}");
                    data = new SaveData();
                    Save();
                }
            }
        }

        public void Save()
        {
            if (data == null)
            {
                data = new SaveData();
            }
            if (data.facilityLevels == null)
            {
                data.facilityLevels = new List<LevelEntry>();
            }

            string path = Application.persistentDataPath + "/SaveData.json";
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
        }

        public int GetLevel(string key, int defaultLevel = 1)
        {
            if (data == null) Load();
            if (data.facilityLevels == null) data.facilityLevels = new List<LevelEntry>();

            var entry = data.facilityLevels.Find(e => e.key == key);
            if (entry != null)
            {
                return entry.level;
            }

            data.facilityLevels.Add(new LevelEntry(key, defaultLevel));
            Save();
            return defaultLevel;
        }

        public void SetLevel(string key, int level)
        {
            if (data == null) Load();
            if (data.facilityLevels == null) data.facilityLevels = new List<LevelEntry>();

            var entry = data.facilityLevels.Find(e => e.key == key);
            if (entry != null)
            {
                entry.level = level;
            }
            else
            {
                data.facilityLevels.Add(new LevelEntry(key, level));
            }

            Save();
        }

        public int UpgradeLevel(string key, int maxLevel = 10)
        {
            if (data == null) Load();
            if (data.facilityLevels == null) data.facilityLevels = new List<LevelEntry>();

            var entry = data.facilityLevels.Find(e => e.key == key);
            if (entry == null)
            {
                entry = new LevelEntry(key, 1);
                data.facilityLevels.Add(entry);
            }

            if (entry.level < maxLevel)
            {
                entry.level++;
                Save();
                Debug.Log($"[SaveManager] '{key}' 업그레이드 완료! 현재 레벨: {entry.level}");
            }
            else
            {
                Debug.LogWarning($"[SaveManager] '{key}'이(가) 이미 최대 레벨({maxLevel})입니다.");
            }

            return entry.level;
        }

        public int UpgradeBurgerMachine()
        {
            if (data == null)
            {
                Load();
            }

            if (data.burgerMachineLevel < 10)
            {
                data.burgerMachineLevel++;
                Save();
                Debug.Log($"[SaveManager] 버거 머신 업그레이드 완료! 현재 레벨: {data.burgerMachineLevel}");
            }
            else
            {
                Debug.LogWarning("[SaveManager] 버거 머신이 이미 최대 레벨(10)입니다.");
            }

            return data.burgerMachineLevel;
        }
    }
}
