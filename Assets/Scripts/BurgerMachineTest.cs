using UnityEngine;
using MyCSVSystem;
using UnityEngine.InputSystem;

public class BurgerMachineTest : MonoBehaviour
{
    [SerializeField]
    public float ProduceTime;

    [SerializeField]
    public int Capacity;

    private EntryCloner cloner;

    private void Start()
    {
        cloner = GetComponent<EntryCloner>();

        if (cloner == null)
        {
            Debug.LogError($"[{gameObject.name}] EntryCloner를 찾을 수 없습니다.");
            return;
        }

        RefreshStats();
        Debug.Log($"[{gameObject.name}] CSV 연동 완료! ProduceTime: {ProduceTime}, Capacity: {Capacity}");
    }

    private void Update()
    {
        // 최신 Input System 패키지 키보드 입력 체크
        bool isSpacePressed = false;
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            isSpacePressed = true;
        }

        if (isSpacePressed)
        {
            if (cloner == null)
            {
                cloner = GetComponent<EntryCloner>();
                if (cloner == null) return;
            }

            int newLevel = cloner.UpgradeLevel();
            RefreshStats();

            Debug.Log($"[{gameObject.name}] 업그레이드 반영 완료! Level: {newLevel}, ProduceTime: {ProduceTime}, Capacity: {Capacity}");
        }
    }

    private void RefreshStats()
    {
        ProduceTime = cloner.GetStat<float>("ProduceTime");
        Capacity = cloner.GetStat<int>("Capacity");
    }
}
