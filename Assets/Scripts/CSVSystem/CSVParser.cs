using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MyCSVSystem
{
    public static class CSVParser
    {
        /// <summary>
        /// RFC 4180 표준을 준수하는 CSV 파서.
        /// 큰따옴표 안의 쉼표(,) 및 줄바꿈(\n, \r\n)을 구분자로 취급하지 않고 데이터로 유지하며,
        /// 이스케이프된 ""를 "로 변환합니다.
        /// </summary>
        public static List<List<string>> ParseCSV(string csvText)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(csvText))
                return rows;

            var currentRow = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;
            int length = csvText.Length;

            for (int i = 0; i < length; i++)
            {
                char c = csvText[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // "" -> " 이스케이프 처리
                        if (i + 1 < length && csvText[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else if (c == '\r')
                    {
                        if (i + 1 < length && csvText[i + 1] == '\n')
                        {
                            i++;
                        }
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();

                        if (!IsRowEmpty(currentRow))
                        {
                            rows.Add(currentRow);
                        }
                        currentRow = new List<string>();
                    }
                    else if (c == '\n')
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();

                        if (!IsRowEmpty(currentRow))
                        {
                            rows.Add(currentRow);
                        }
                        currentRow = new List<string>();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            if (inQuotes)
            {
                Debug.LogWarning($"[CSVParser] 닫히지 않은 큰따옴표가 있습니다. {rows.Count + 1}번째 행 이후 데이터가 한 셀로 합쳐졌을 수 있습니다.");
            }

            if (currentRow.Count > 0 || currentField.Length > 0)
            {
                currentRow.Add(currentField.ToString());
                if (!IsRowEmpty(currentRow))
                {
                    rows.Add(currentRow);
                }
            }

            return rows;
        }

        /// <summary>
        /// 단일 라인에 대한 RFC 4180 토크나이저.
        /// </summary>
        public static List<string> TokenizeLine(string line)
        {
            var fields = new List<string>();
            if (line == null)
                return fields;

            var currentField = new StringBuilder();
            bool inQuotes = false;
            int length = line.Length;

            for (int i = 0; i < length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < length && line[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            fields.Add(currentField.ToString());
            return fields;
        }

        private static object Invalid(string type, string rawValue, object fallback)
        {
            Debug.LogWarning($"[CSVParser] '{rawValue}'을(를) {type}(으)로 읽을 수 없어 {fallback}(으)로 처리합니다.");
            return fallback;
        }

        private static bool IsRowEmpty(List<string> row)
        {
            if (row == null || row.Count == 0) return true;
            if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) return true;
            return false;
        }

        /// <summary>
        /// rawValue를 타입에 맞게 안전하게 파싱합니다.
        /// rawValue가 비어있거나 빈칸(,,)일 경우 타입별 안전 기본값 반환.
        /// </summary>
        public static object ParseValue(string type, string rawValue)
        {
            string cleanType = type?.Trim().ToLowerInvariant() ?? "";
            bool isEmpty = string.IsNullOrWhiteSpace(rawValue);

            switch (cleanType)
            {
                case "int":
                case "int32":
                {
                    if (isEmpty) return 0;
                    if (int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int intVal))
                        return intVal;
                    return Invalid(type, rawValue, 0);
                }

                case "int64":
                case "long":
                {
                    if (isEmpty) return 0L;
                    if (long.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long longVal))
                        return longVal;
                    return Invalid(type, rawValue, 0L);
                }

                case "float":
                case "single":
                {
                    if (isEmpty) return 0.0f;
                    string clean = rawValue.Trim().TrimEnd('f', 'F');
                    if (float.TryParse(clean, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float fVal))
                        return fVal;
                    return Invalid(type, rawValue, 0.0f);
                }

                case "double":
                {
                    if (isEmpty) return 0.0;
                    string clean = rawValue.Trim().TrimEnd('d', 'D', 'f', 'F');
                    if (double.TryParse(clean, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double dVal))
                        return dVal;
                    return Invalid(type, rawValue, 0.0);
                }

                case "bool":
                case "boolean":
                {
                    if (isEmpty) return false;
                    string bStr = rawValue.Trim();
                    if (bool.TryParse(bStr, out bool bVal))
                        return bVal;
                    if (bStr == "1") return true;
                    if (bStr == "0") return false;
                    return Invalid(type, rawValue, false);
                }

                case "string":
                {
                    return rawValue ?? string.Empty;
                }

                default:
                {
                    Debug.LogWarning($"[CSVParser] 지원하지 않는 타입입니다: {type}. 문자열로 처리합니다.");
                    return rawValue ?? string.Empty;
                }
            }
        }
    }
}
