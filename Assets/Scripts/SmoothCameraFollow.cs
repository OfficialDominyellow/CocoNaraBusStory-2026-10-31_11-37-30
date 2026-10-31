using UnityEngine;

public class SmoothCameraFollow : MonoBehaviour
{
    [Header("추적 대상 (꼬마버스)")]
    public Transform target;

    [Header("카메라 위치 세팅 (유아용 쿼터뷰 각도)")]
    public Vector3 offset = new Vector3(0f, 9.5f, -13.5f);
    public float positionSmoothTime = 0.22f;
    public float rotationSmoothSpeed = 4.5f;
    public float lookAheadDistance = 2.5f;

    private Vector3 currentVelocity;

    private void Start()
    {
        if (target == null && BusController.Instance != null)
        {
            target = BusController.Instance.transform;
        }

        if (target != null)
        {
            Vector3 desiredPosition = target.position + target.TransformDirection(offset);
            transform.position = desiredPosition;
            transform.LookAt(target.position + target.forward * lookAheadDistance);
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            if (BusController.Instance != null)
            {
                target = BusController.Instance.transform;
            }
            return;
        }

        // 버스 후방 대각선 상단 위치 계산
        Vector3 desiredPosition = target.position + target.TransformDirection(offset);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, positionSmoothTime);

        // 버스의 앞쪽 약간 먼 곳을 자연스럽게 바라보도록 보간 회전
        Vector3 lookTarget = target.position + target.forward * lookAheadDistance + Vector3.up * 1.0f;
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * Time.deltaTime);
    }
}

