using UnityEngine;

public class SmoothCameraFollow : MonoBehaviour
{
    [Header("추적 대상 (꼬마버스)")]
    public Transform target;

    [Header("카메라 위치 세팅 (유아용 시원한 쿼터뷰 각도)")]
    public Vector3 offset = new Vector3(0f, 10.5f, -14.5f);
    public float positionSmoothTime = 0.18f;
    public float rotationSmoothSpeed = 5.5f;
    public float lookAheadDistance = 4.0f;
    public float lookUpOffset = 1.2f;

    private Vector3 currentVelocity;

    private void Start()
    {
        if (target == null && BusController.Instance != null)
        {
            target = BusController.Instance.transform;
        }

        SnapToTarget();
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        Vector3 desiredPosition = target.position + target.TransformDirection(offset);
        transform.position = desiredPosition;
        transform.LookAt(target.position + target.forward * lookAheadDistance + Vector3.up * lookUpOffset);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            if (BusController.Instance != null)
            {
                target = BusController.Instance.transform;
                SnapToTarget();
            }
            return;
        }

        // 버스 후방 대각선 상단 위치 계산
        Vector3 desiredPosition = target.position + target.TransformDirection(offset);

        // 시야 가림 방지: 버스와 카메라 사이에 장애물이 있으면 카메라를 위로 띄워 시야 확보
        RaycastHit hit;
        Vector3 busCenter = target.position + Vector3.up * 1.5f;
        Vector3 camDir = desiredPosition - busCenter;
        if (Physics.Raycast(busCenter, camDir.normalized, out hit, camDir.magnitude))
        {
            if (hit.collider != null && !hit.collider.isTrigger && !hit.transform.IsChildOf(target))
            {
                // 충돌 지점 약간 앞 또는 높은 곳으로 부드럽게 조정
                desiredPosition = hit.point + hit.normal * 0.5f + Vector3.up * 1.0f;
            }
        }

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, positionSmoothTime);

        // 버스의 앞쪽 도로를 시원하게 내려다보는 각도
        Vector3 lookTarget = target.position + target.forward * lookAheadDistance + Vector3.up * lookUpOffset;
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * Time.deltaTime);
    }
}
