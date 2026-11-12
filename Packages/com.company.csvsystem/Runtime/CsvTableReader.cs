using System.Collections.Generic;

namespace MyCSVSystem
{
    /// <summary>
    /// CSV 텍스트를 TableData로 바꿉니다. MonoBehaviour가 필요 없어 에디터 임포터와 DatabaseManager가 함께 씁니다.
    /// 1행 = 컬럼 이름, 2행 = 타입, 3행부터 데이터. A열이 행 ID입니다.
    /// </summary>
    public static class CsvTableReader
    {
        /// <param name="errors">
        /// 데이터 부족, 중복 ID, 닫히지 않은 따옴표, 타입에 맞지 않는 셀을 여기에 추가합니다 (셀은 기본값으로 채워짐).
        /// null이면 경고 로그로 남깁니다.
        /// </param>
        /// <returns>헤더·타입 행이 없으면 null</returns>
        public static TableData Read(string csvText, string tableName, List<string> errors = null)
        {
            var parseErrors = new List<string>();
            List<List<string>> rows = CSVParser.ParseCSV(csvText, parseErrors);
            foreach (string error in parseErrors)
                Report(errors, $"{tableName}: {error}");

            if (rows == null || rows.Count < 2)
            {
                Report(errors, $"{tableName}: 헤더(1행)와 타입(2행)이 없습니다.");
                return null;
            }

            List<string> headers = rows[0];
            List<string> types = rows[1];
            var table = new TableData();

            for (int i = 2; i < rows.Count; i++)
            {
                List<string> values = rows[i];
                if (IsAllEmpty(values)) continue;

                var row = new RowEntity();
                for (int j = 0; j < headers.Count; j++)
                {
                    string header = headers[j].Trim();
                    if (string.IsNullOrEmpty(header)) continue;

                    // 부족한 필드는 빈칸으로 채운다 (사일런트 행 누락 방지)
                    string raw = j < values.Count ? values[j] : string.Empty;
                    string colType = j < types.Count ? types[j].Trim() : "string";
                    if (!CSVParser.TryParseValue(colType, raw, out object value, out _))
                        Report(errors, $"{tableName}: {i + 1}행 '{header}' 열 값 '{raw}'을(를) {colType}(으)로 읽을 수 없습니다.");
                    row.SetField(header, value, colType);
                }

                string rowId = values.Count > 0 ? values[0].Trim() : string.Empty;
                if (string.IsNullOrEmpty(rowId))
                {
                    Report(errors, $"{tableName}: {i + 1}행의 ID(A열)가 비어 있어 건너뜁니다.");
                    continue;
                }
                if (!table.AddRow(rowId, row))
                    Report(errors, $"{tableName}: ID '{rowId}'가 중복되어 앞의 행을 덮어씁니다 ({i + 1}행).");
            }

            return table;
        }

        private static void Report(List<string> errors, string message)
        {
            if (errors != null) errors.Add(message);
            else UnityEngine.Debug.LogWarning($"[CsvTableReader] {message}");
        }

        private static bool IsAllEmpty(List<string> values)
        {
            for (int k = 0; k < values.Count; k++)
            {
                if (!string.IsNullOrWhiteSpace(values[k])) return false;
            }
            return true;
        }
    }
}
