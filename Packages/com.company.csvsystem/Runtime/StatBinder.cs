using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MyCSVSystem
{
    /// <summary>
    /// EntryCloner의 CSV 값을 다른 컴포넌트의 필드/프로퍼티에 주입합니다.
    /// 대상 스크립트를 수정하지 않아도 되며, 레벨이 바뀔 때마다 다시 주입합니다.
    /// 대상의 Awake보다 먼저 실행되도록 실행 순서를 앞당겨 두었습니다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class StatBinder : MonoBehaviour
    {
        [Serializable]
        public class Binding
        {
            [Tooltip("값을 넣을 컴포넌트")]
            public Component target;

            [Tooltip("대상의 필드 또는 프로퍼티 이름 (private 가능)")]
            public string memberName;

            [Tooltip("CSV 컬럼 이름")]
            public string column;
        }

        [Tooltip("비워두면 같은 오브젝트의 EntryCloner를 사용")]
        public EntryCloner cloner;

        public List<Binding> bindings = new List<Binding>();

        private const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private void Awake()
        {
            if (cloner == null) cloner = GetComponent<EntryCloner>();
            if (cloner == null)
            {
                Debug.LogError($"[StatBinder] {gameObject.name}에서 EntryCloner를 찾을 수 없습니다.", this);
                return;
            }

            cloner.StatsApplied += Apply;

            if (cloner.IsInitialized) Apply();
            else if (cloner.autoInit) cloner.EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (cloner != null) cloner.StatsApplied -= Apply;
        }

        /// <summary>
        /// 현재 CSV 값을 모든 대상에 다시 주입합니다. CSV에 없는 컬럼은 대상의 기존 값을 유지합니다.
        /// </summary>
        public void Apply()
        {
            if (cloner == null) return;

            foreach (var binding in bindings)
            {
                if (binding == null || binding.target == null || string.IsNullOrEmpty(binding.memberName)) continue;

                if (!cloner.TryGetStat<object>(binding.column, out object raw))
                {
                    Debug.LogWarning($"[StatBinder] 컬럼 '{binding.column}'이(가) 현재 행에 없어 {binding.memberName}의 기존 값을 유지합니다. ({gameObject.name})", this);
                    continue;
                }

                if (!TryAssign(binding.target, binding.memberName, raw, out string error))
                {
                    Debug.LogWarning($"[StatBinder] {binding.target.GetType().Name}.{binding.memberName}: {error} ({gameObject.name})", this);
                }
            }
        }

        private static bool TryAssign(object target, string memberName, object raw, out string error)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(memberName, MemberFlags);
                if (field != null)
                {
                    if (field.IsLiteral || field.IsInitOnly)
                    {
                        error = "const/readonly 필드는 값을 넣을 수 없습니다.";
                        return false;
                    }
                    if (!StatValue.TryConvert(raw, field.FieldType, out object value))
                    {
                        error = $"값 '{raw}'을(를) {field.FieldType.Name}(으)로 변환할 수 없습니다.";
                        return false;
                    }
                    field.SetValue(target, value);
                    error = null;
                    return true;
                }

                PropertyInfo property = type.GetProperty(memberName, MemberFlags);
                if (property != null)
                {
                    if (property.GetSetMethod(true) == null)
                    {
                        error = "setter가 없는 프로퍼티입니다.";
                        return false;
                    }
                    if (!StatValue.TryConvert(raw, property.PropertyType, out object value))
                    {
                        error = $"값 '{raw}'을(를) {property.PropertyType.Name}(으)로 변환할 수 없습니다.";
                        return false;
                    }
                    property.SetValue(target, value);
                    error = null;
                    return true;
                }
            }

            error = "해당 이름의 필드/프로퍼티를 찾을 수 없습니다.";
            return false;
        }
    }
}
