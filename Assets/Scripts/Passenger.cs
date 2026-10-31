using TMPro;
using UnityEngine;

public class Passenger : MonoBehaviour
{
    [Header("승객 기본 정보")]
    public string passengerName = "선율이";
    public string destinationLandmarkName = "나라유치원";
    public Texture2D faceTexture;
    public bool isBoarded = false;

    [Header("2.5D 비주얼 & 애니메이션")]
    public Transform visualRoot;
    public MeshRenderer portraitRenderer;
    public TextMeshPro nameTagText;
    public float bounceSpeed = 3.5f;
    public float bounceHeight = 0.2f;

    [Header("이펙트")]
    public ParticleSystem sparkleParticle;

    private Vector3 initialVisualPos;
    private float animOffset;

    private void Start()
    {
        animOffset = Random.Range(0f, Mathf.PI * 2f);
        if (visualRoot != null)
        {
            initialVisualPos = visualRoot.localPosition;
        }

        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        if (nameTagText != null)
        {
            nameTagText.text = $"{passengerName}\n<size=70%><color=#FFE680>▶ {destinationLandmarkName}</color></size>";
        }

        if (portraitRenderer != null && faceTexture != null)
        {
            portraitRenderer.material.mainTexture = faceTexture;
        }
    }

    private void Update()
    {
        if (isBoarded) return;

        // 아이들 눈에 잘 띄도록 콩콩 뛰는 귀여운 바운스 애니메이션
        if (visualRoot != null)
        {
            float bounceY = Mathf.Abs(Mathf.Sin((Time.time + animOffset) * bounceSpeed)) * bounceHeight;
            visualRoot.localPosition = initialVisualPos + Vector3.up * bounceY;

            float wobbleZ = Mathf.Sin((Time.time + animOffset) * bounceSpeed * 0.5f) * 4f;
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, wobbleZ);
        }
    }

    private void LateUpdate()
    {
        if (isBoarded) return;

        // 카메라를 항상 정면으로 바라보는 2.5D Billboard
        Camera cam = Camera.main;
        if (cam != null && visualRoot != null)
        {
            visualRoot.forward = cam.transform.forward;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isBoarded) return;

        BusController bus = other.GetComponentInParent<BusController>();
        if (bus != null && PassengerManager.Instance != null)
        {
            PassengerManager.Instance.TryBoardPassenger(this);
        }
    }

    /// <summary>
    /// 버스 탑승 처리
    /// </summary>
    public void OnBoarded()
    {
        isBoarded = true;
        if (sparkleParticle != null)
        {
            sparkleParticle.Play();
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 목적지 도착 후 하차 처리
    /// </summary>
    public void OnDroppedOff(Vector3 dropPosition)
    {
        isBoarded = false;
        transform.position = dropPosition;
        gameObject.SetActive(true);

        if (sparkleParticle != null)
        {
            sparkleParticle.Play();
        }
    }
}

