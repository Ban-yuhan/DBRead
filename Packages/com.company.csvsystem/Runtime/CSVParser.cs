using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MyCSVSystem
{
    /// <summary>
    /// CSV 텍스트 파서와 셀 값 변환.
    /// 문제(닫히지 않은 따옴표, 타입에 맞지 않는 값)는 errors 목록을 넘기면 거기에 모으고, 넘기지 않으면 경고 로그로 남깁니다.
    /// 엄격하게 막을지는 호출하는 쪽이 정합니다: 에디터 임포터는 오류로 처리하고, 런타임 DatabaseManager는 경고 후 기본값으로 진행합니다.
    /// </summary>
    public static class CSVParser
    {
        private const char Bom = '﻿';

        /// <summary>
        /// RFC 4180 표준을 준수하는 CSV 파서.
        /// 큰따옴표 안의 쉼표(,) 및 줄바꿈(\n, \r\n)을 구분자로 취급하지 않고 데이터로 유지하며,
        /// 이스케이프된 ""를 "로 변환합니다.
        /// </summary>
        public static List<List<string>> ParseCSV(string csvText) => ParseCSV(csvText, null);

        /// <param name="errors">null이 아니면 닫히지 않은 따옴표 같은 문제를 로그 대신 여기에 추가합니다.</param>
        public static List<List<string>> ParseCSV(string csvText, List<string> errors)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(csvText))
                return rows;

            // Excel이 저장한 UTF-8 BOM은 첫 헤더 이름에 붙으므로 제거한다
            if (csvText[0] == Bom)
                csvText = csvText.Substring(1);

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
                    else if (c == '\r' || c == '\n')
                    {
                        if (c == '\r' && i + 1 < length && csvText[i + 1] == '\n')
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
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            if (inQuotes)
            {
                Report(errors, $"닫히지 않은 큰따옴표가 있습니다. {rows.Count + 1}번째 행 이후 데이터가 한 셀로 합쳐졌을 수 있습니다.");
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

        private static bool IsRowEmpty(List<string> row)
        {
            if (row == null || row.Count == 0) return true;
            if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) return true;
            return false;
        }

        private static void Report(List<string> errors, string message)
        {
            if (errors != null) errors.Add(message);
            else Debug.LogWarning($"[CSVParser] {message}");
        }

        /// <summary>
        /// rawValue를 타입에 맞게 파싱합니다. 빈칸은 타입별 기본값(0, false, "")입니다.
        /// 읽을 수 없는 값이면 경고 로그를 남기고 기본값을 반환합니다. (런타임용: 멈추지 않음)
        /// </summary>
        public static object ParseValue(string type, string rawValue)
        {
            if (!TryParseValue(type, rawValue, out object value, out string error))
                Debug.LogWarning($"[CSVParser] {error}");
            return value;
        }

        /// <summary>
        /// rawValue를 타입에 맞게 파싱합니다. 빈칸은 타입별 기본값이며 성공으로 봅니다 (빈칸 = 0 규칙).
        /// 값이 타입에 맞지 않거나 지원하지 않는 타입이면 false와 이유를 돌려주고, value에는 기본값을 넣습니다.
        /// </summary>
        public static bool TryParseValue(string type, string rawValue, out object value, out string error)
        {
            string cleanType = type?.Trim().ToLowerInvariant() ?? "";
            bool isEmpty = string.IsNullOrWhiteSpace(rawValue);
            string text = isEmpty ? string.Empty : rawValue.Trim();
            error = null;

            switch (cleanType)
            {
                case "int":
                case "int32":
                    value = 0;
                    if (isEmpty) return true;
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intVal)) { value = intVal; return true; }
                    break;

                case "int64":
                case "long":
                    value = 0L;
                    if (isEmpty) return true;
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longVal)) { value = longVal; return true; }
                    break;

                case "float":
                case "single":
                    value = 0.0f;
                    if (isEmpty) return true;
                    if (float.TryParse(text.TrimEnd('f', 'F'), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float fVal)) { value = fVal; return true; }
                    break;

                case "double":
                    value = 0.0;
                    if (isEmpty) return true;
                    if (double.TryParse(text.TrimEnd('d', 'D', 'f', 'F'), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double dVal)) { value = dVal; return true; }
                    break;

                case "bool":
                case "boolean":
                    value = false;
                    if (isEmpty) return true;
                    if (bool.TryParse(text, out bool bVal)) { value = bVal; return true; }
                    if (text == "1") { value = true; return true; }
                    if (text == "0") { value = false; return true; }
                    break;

                case "string":
                    value = rawValue ?? string.Empty;
                    return true;

                default:
                    value = rawValue ?? string.Empty;
                    error = $"지원하지 않는 타입 '{type}'입니다. 문자열로 처리합니다.";
                    return false;
            }

            error = $"'{rawValue}'을(를) {type}(으)로 읽을 수 없어 {value}(으)로 처리합니다.";
            return false;
        }
    }
}
