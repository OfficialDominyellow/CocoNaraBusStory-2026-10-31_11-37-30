using TMPro;
using UnityEngine;

public class Landmark : MonoBehaviour
{
    [Header("랜드마크 정보")]
    public string landmarkName = "아이파크캐슬";
    public string subtitle = "우리집 🏠";
    public bool isEVChargingStation = false;

    [Header("3D 간판 & 비주얼")]
    public Transform signRoot;
    public TextMeshPro signTitleTMP;
    public TextMeshPro signSubtitleTMP;
    public ParticleSystem arrivalParticle;

    private float initialSignY;

    private void Start()
    {
        if (signRoot != null)
        {
            initialSignY = signRoot.localPosition.y;
        }

        UpdateSignText();
    }

    public void UpdateSignText()
    {
        if (signTitleTMP != null)
        {
            signTitleTMP.text = landmarkName;
        }
        if (signSubtitleTMP != null)
        {
            signSubtitleTMP.text = subtitle;
        }
    }

    private void Update()
    {
        // 3D 간판 살랑살랑 플로팅 연출
        if (signRoot != null)
        {
            float floatY = Mathf.Sin(Time.time * 2.2f) * 0.25f;
            signRoot.localPosition = new Vector3(signRoot.localPosition.x, initialSignY + floatY, signRoot.localPosition.z);
        }
    }

    private void LateUpdate()
    {
        // 간판이 카메라 방향을 부드럽게 향하도록 빌보드 처리
        Camera cam = Camera.main;
        if (cam != null && signRoot != null)
        {
            Vector3 lookDir = cam.transform.position - signRoot.position;
            lookDir.y = 0f; // 수평 회전만 유지
            if (lookDir.sqrMagnitude > 0.01f)
            {
                signRoot.rotation = Quaternion.LookRotation(-lookDir);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        BusController bus = other.GetComponentInParent<BusController>();
        if (bus == null) return;

        if (isEVChargingStation)
        {
            bus.SetChargingState(true);
        }

        if (arrivalParticle != null)
        {
            arrivalParticle.Play();
        }

        if (PassengerManager.Instance != null)
        {
            PassengerManager.Instance.OnArrivedAtLandmark(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        BusController bus = other.GetComponentInParent<BusController>();
        if (bus == null) return;

        if (isEVChargingStation)
        {
            bus.SetChargingState(false);
        }
    }

    public void PlayCelebration()
    {
        if (arrivalParticle != null)
        {
            arrivalParticle.Play();
        }
    }
}

