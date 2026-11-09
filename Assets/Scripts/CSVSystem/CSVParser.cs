using UnityEngine;
using System;

namespace MyCSVSystem
{

    public class CSVParser : MonoBehaviour
    {
        public static object ParseValue(string type, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            switch (type.ToLower().Trim())
            {
                case "int":
                case "int32":
                    {
                        return int.Parse(value);
                    }

                case "int64":
                case "long":
                    {
                        return long.Parse(value);
                    }

                case "float":
                    {
                        return float.Parse(value);
                    }

                case "double":
                    {
                        return double.Parse(value);
                    }

                case "bool":
                    {
                        return bool.Parse(value);
                    }

                case "string":
                    {
                        return value;
                    }

                default:
                    {
                        Debug.LogWarning($"[CSVParser] 알 수 없는 타입입니다: {type}. 문자열로 처리합니다");
                        return value;
                    }
            }
        }
    }
}
