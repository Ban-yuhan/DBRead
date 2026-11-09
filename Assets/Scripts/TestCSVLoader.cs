using UnityEngine;
using MyCSVSystem;

public class TestCSVLoader : MonoBehaviour
{
    private void Start()
    {
        // 게임 켜지자마자 "Resources/Data/FacilityData.csv"를 읽어와서 "Facility" 서랍장으로 등록!
        DatabaseManager.Instance.LoadTable("Facility", "Data/FacilityData");
    }
}