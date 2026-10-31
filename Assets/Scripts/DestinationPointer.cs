using UnityEngine;

public class DestinationPointer : MonoBehaviour
{
    public static DestinationPointer Instance { get; private set; }

    [Header("화살표 세팅")]
    public Transform arrowVisual;
    public Vector3 targetPosition;
    public float hoverHeight = 4.2f;
    public float floatSpeed = 2.8f;
    public float floatAmount = 0.35f;

    private float initialY;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (arrowVisual != null)
        {
            initialY = arrowVisual.localPosition.y;
        }
    }

    private void Update()
    {
        // 버스 머리 위에서 살랑살랑 플로팅
        if (arrowVisual != null)
        {
            float newY = initialY + Mathf.Sin(Time.time * floatSpeed) * floatAmount;
            arrowVisual.localPosition = new Vector3(arrowVisual.localPosition.x, newY, arrowVisual.localPosition.z);
        }

        // 목적지 방향 업데이트
        UpdateTargetPosition();

        if (arrowVisual != null)
        {
            Vector3 dir = targetPosition - transform.position;
            dir.y = 0f; // 수평 평면 방향
            if (dir.sqrMagnitude > 1.0f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                arrowVisual.rotation = Quaternion.Slerp(arrowVisual.rotation, targetRot, Time.deltaTime * 6f);
            }
        }
    }

    private void UpdateTargetPosition()
    {
        if (PassengerManager.Instance == null) return;

        // 1. 탑승한 승객이 있으면 해당 승객의 목적지 랜드마크를 가리킴
        if (PassengerManager.Instance.boardedPassengers.Count > 0)
        {
            Passenger p = PassengerManager.Instance.boardedPassengers[0];
            Landmark[] allLM = FindObjectsByType<Landmark>(FindObjectsSortMode.None);
            foreach (var lm in allLM)
            {
                if (lm.landmarkName == p.destinationLandmarkName)
                {
                    targetPosition = lm.transform.position;
                    return;
                }
            }
        }

        // 2. 탑승한 승객이 없으면 남아있는 가장 가까운 정류장 승객을 가리킴
        float closestDist = float.MaxValue;
        Vector3 bestPos = Vector3.zero;
        bool found = false;

        foreach (var p in PassengerManager.Instance.allWorldPassengers)
        {
            if (p != null && !p.isBoarded && p.gameObject.activeInHierarchy)
            {
                float dist = Vector3.Distance(transform.position, p.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestPos = p.transform.position;
                    found = true;
                }
            }
        }

        if (found)
        {
            targetPosition = bestPos;
        }
    }
}

