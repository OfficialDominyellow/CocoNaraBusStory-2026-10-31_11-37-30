using UnityEngine;

public class AirportPlaneAnimation : MonoBehaviour
{
    [Header("비행기 순환 비행 세팅")]
    public Transform planeModel;
    public Vector3 runwayStartPos = new Vector3(-20f, 1f, -10f);
    public Vector3 runwayEndPos = new Vector3(20f, 1f, -10f);
    public float flightRadius = 35f;
    public float flightAltitude = 18f;
    public float flightSpeed = 12f;

    private float timer = 0f;

    private void Update()
    {
        if (planeModel == null) return;

        timer += Time.deltaTime * (flightSpeed / flightRadius);

        // 공항 상공을 부드럽게 선회 비행
        float x = Mathf.Sin(timer) * flightRadius;
        float z = Mathf.Cos(timer) * (flightRadius * 0.7f);
        float y = flightAltitude + Mathf.Sin(timer * 2f) * 2.5f;

        Vector3 nextPos = transform.position + new Vector3(x, y, z);
        Vector3 forwardDir = (nextPos - planeModel.position).normalized;

        planeModel.position = nextPos;
        if (forwardDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(forwardDir);
            // 비행기 뱅크각(기울기) 연출
            targetRot *= Quaternion.Euler(0f, 0f, -Mathf.Sin(timer) * 25f);
            planeModel.rotation = Quaternion.Slerp(planeModel.rotation, targetRot, Time.deltaTime * 5f);
        }
    }
}

