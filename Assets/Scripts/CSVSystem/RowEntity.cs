using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyCSVSystem
{
    //가로 1줄 분량의 데이터들을 이름표를 붙여 안전하게 가지고 있는 보관함 역할
    public class RowEntity : MonoBehaviour
    {
        //컬럼 이름을 key삼아 데이터 저장. ex) ["ProduceTime", "3.5f"]
        private Dictionary<string, object> _data = new Dictionary<string, object>();

        //컬럼 타입 이름도 함께 저장.ex) ["ProduceTime", "float"]
        private Dictionary<string, string> _columnTypes = new Dictionary<string, string>();

        public void SetField(string columnName, object value, string columnType)
        {
            _data[columnName] = value;
            _columnTypes[columnName] = columnType;
        }


        //--------------------------------------------------------------------------------

        //게임 로직이나 다른 스크립트에서 원하는 데이터를 꺼내 쓸 때 호출하는 함수
        public T Get<T>(string columnName)
        {
            //보관함에 해당 컬럼 이름의 데이터가 있는지 확인
            if(_data.TryGetValue(columnName, out object value))
            {
                if (value == null)
                    return default;

                try
                {
                    //꺼낸 object 데이터를 원하는 타입(T)로 안전하게 변환
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    //변환에 실패하면 강제 형변환 시도
                    return (T)value;
                }
            }

            Debug.LogWarning($"[RowEntity] ' {columnName}; 컬럼을 찾을 수 없습니다.");
            return default;
        }

        public IEnumerable<string> GetColumns() => _data.Keys;

        public string GetColumnType(string columnName) => _columnTypes.GetValueOrDefault(columnName, "");

        public bool ContainsColumn(string columnName) => _data.ContainsKey(columnName);
        /*
        => (람다/한줄 줄여쓰기) : 오른쪽 것을 바로 return한다 는 의미.
        아래와 완전히 같이 작동

        public bool COntainsColumn(string columnName)
        {
            return _data.ContainsKey(columnName);
        }
        */
    }
}
