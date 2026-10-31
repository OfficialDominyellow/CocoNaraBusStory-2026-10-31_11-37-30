using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class BusController : MonoBehaviour
{
    public static BusController Instance { get; private set; }

    [Header("주행 세팅 (유아용 부드러운 아케이드 조작)")]
    public float maxForwardSpeed = 22f;
    public float maxReverseSpeed = 10f;
    public float acceleration = 20f;
    public float deceleration = 18f;
    public float steerSpeed = 110f;

    [Header("배터리 시스템 (친환경 꼬마 전기버스)")]
    public float maxBattery = 100f;
    public float currentBattery = 100f;
    public float batteryDrainPerSec = 0.25f; // 주행 시 천천히 감소 (약 6분 주행 가능)
    public bool isCharging = false;

    [Header("표정 연출 (2.5D 앞유리 얼굴)")]
    public MeshRenderer faceRenderer;
    public Texture2D normalFaceTexture;
    public Texture2D winkFaceTexture;
    public Texture2D happyChargingFaceTexture;

    [Header("바퀴 회전 연출")]
    public List<Transform> rotatingWheels = new List<Transform>();
    public List<Transform> steeringFrontWheels = new List<Transform>();
    public float wheelRadius = 0.45f;

    [Header("이펙트 & 오디오")]
    public ParticleSystem dustParticle;
    public ParticleSystem hornHeartParticle;
    public ParticleSystem electricSparkParticle;
    public AudioSource audioSource;

    private Rigidbody rb;
    private float inputThrottle = 0f;
    private float inputSteer = 0f;
    private float currentSpeed = 0f;
    private Coroutine faceExpressionCoroutine;

    private AudioClip hornClip;
    private AudioClip evChargeClip;

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody>();

        // 아이들이 편하게 운전할 수 있도록 전복 방지 설정
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.centerOfMass = new Vector3(0, -0.6f, 0);

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f; // 2D 사운드로 또렷하게
        }

        // 효과음 자동 준비
        hornClip = AudioGenerator.CreateHornClip();
        evChargeClip = AudioGenerator.CreateEVChargeClip();
    }

    private void Start()
    {
        currentBattery = maxBattery;
        SetFaceExpression(normalFaceTexture);
    }

    private void Update()
    {
        ReadInput();
        UpdateBattery();
        UpdateWheelVisuals();
    }

    private void FixedUpdate()
    {
        MoveBus();
    }

    /// <summary>
    /// 키보드(WASD/방향키) 및 모바일 UI 터치 입력을 통합 처리 (New Input System / Legacy Input 완벽 지원)
    /// </summary>
    private void ReadInput()
    {
        // 미니게임 팝업 등이 열려있으면 조작 정지
        if (MiniGameUI.IsActive)
        {
            inputThrottle = 0f;
            inputSteer = 0f;
            return;
        }

        float keyThrottle = 0f;
        float keySteer = 0f;

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) keyThrottle += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) keyThrottle -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keySteer -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keySteer += 1f;

            if (kb.spaceKey.wasPressedThisFrame)
            {
                HonkHorn();
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) keyThrottle += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) keyThrottle -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keySteer -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keySteer += 1f;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                HonkHorn();
            }
        }
        catch
        {
        }
#endif

        // UI 터치 입력과 키보드 입력을 합성
        float finalThrottle = Mathf.Clamp(inputThrottle + keyThrottle, -1f, 1f);
        float finalSteer = Mathf.Clamp(inputSteer + keySteer, -1f, 1f);

        // 배터리 방전 시 가속 불가
        if (currentBattery <= 0f && finalThrottle > 0f)
        {
            finalThrottle = 0f;
        }

        inputThrottle = finalThrottle;
        inputSteer = finalSteer;
    }

    /// <summary>
    /// 모바일 온스크린 가속/브레이크 버튼 연동
    /// </summary>
    public void SetTouchThrottle(float value)
    {
        inputThrottle = value;
    }

    /// <summary>
    /// 모바일 온스크린 좌우 조향 버튼 연동
    /// </summary>
    public void SetTouchSteer(float value)
    {
        inputSteer = value;
    }

    private void MoveBus()
    {
        float targetSpeed = 0f;
        if (inputThrottle > 0.05f)
        {
            targetSpeed = inputThrottle * maxForwardSpeed;
        }
        else if (inputThrottle < -0.05f)
        {
            targetSpeed = inputThrottle * maxReverseSpeed;
        }

        // 부드러운 가속 및 감속
        float accelRate = (Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed)) ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.fixedDeltaTime);

        // 전진 및 후진 이동
        Vector3 moveVelocity = transform.forward * currentSpeed;
        rb.linearVelocity = new Vector3(moveVelocity.x, rb.linearVelocity.y, moveVelocity.z);

        // 회전 (차가 움직일 때만 자연스럽게 회전)
        if (Mathf.Abs(currentSpeed) > 0.3f && Mathf.Abs(inputSteer) > 0.05f)
        {
            float directionSign = Mathf.Sign(currentSpeed);
            float turnAmount = inputSteer * steerSpeed * directionSign * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }

        // 먼지 파티클 연출
        if (dustParticle != null)
        {
            if (Mathf.Abs(currentSpeed) > 2f && !dustParticle.isPlaying)
            {
                dustParticle.Play();
            }
            else if (Mathf.Abs(currentSpeed) <= 2f && dustParticle.isPlaying)
            {
                dustParticle.Stop();
            }
        }
    }

    private void UpdateBattery()
    {
        if (isCharging)
        {
            currentBattery = Mathf.Min(maxBattery, currentBattery + 45f * Time.deltaTime);
            if (currentBattery >= maxBattery && electricSparkParticle != null && electricSparkParticle.isPlaying)
            {
                electricSparkParticle.Stop();
            }
        }
        else if (Mathf.Abs(currentSpeed) > 1f)
        {
            currentBattery = Mathf.Max(0f, currentBattery - batteryDrainPerSec * Time.deltaTime);
        }
    }

    private void UpdateWheelVisuals()
    {
        float rotationDegree = (currentSpeed * Time.deltaTime / (2 * Mathf.PI * wheelRadius)) * 360f;

        foreach (var wheel in rotatingWheels)
        {
            if (wheel != null)
            {
                wheel.Rotate(Vector3.right, rotationDegree, Space.Self);
            }
        }

        float targetSteerAngle = inputSteer * 28f;
        foreach (var steerWheel in steeringFrontWheels)
        {
            if (steerWheel != null)
            {
                Vector3 localEuler = steerWheel.localEulerAngles;
                localEuler.y = Mathf.LerpAngle(localEuler.y, targetSteerAngle, Time.deltaTime * 12f);
                steerWheel.localEulerAngles = localEuler;
            }
        }
    }

    /// <summary>
    /// "빵빵!" 경적 버튼 클릭 시 실행
    /// </summary>
    public void HonkHorn()
    {
        if (audioSource != null && hornClip != null)
        {
            audioSource.PlayOneShot(hornClip, 0.9f);
        }

        // 하트/음표 파티클 방출
        if (hornHeartParticle != null)
        {
            hornHeartParticle.Play();
        }

        // 표정을 윙크로 일시 변경
        if (winkFaceTexture != null)
        {
            TriggerTemporaryFace(winkFaceTexture, 1.4f);
        }
    }

    /// <summary>
    /// 전기차 충전소 진입/이탈 처리
    /// </summary>
    public void SetChargingState(bool charging)
    {
        isCharging = charging;
        if (charging)
        {
            if (audioSource != null && evChargeClip != null)
            {
                audioSource.PlayOneShot(evChargeClip, 0.85f);
            }
            if (electricSparkParticle != null)
            {
                electricSparkParticle.Play();
            }
            if (happyChargingFaceTexture != null)
            {
                SetFaceExpression(happyChargingFaceTexture);
            }
        }
        else
        {
            if (electricSparkParticle != null)
            {
                electricSparkParticle.Stop();
            }
            SetFaceExpression(normalFaceTexture);
        }
    }

    public void TriggerTemporaryFace(Texture2D tempTexture, float duration)
    {
        if (faceExpressionCoroutine != null)
        {
            StopCoroutine(faceExpressionCoroutine);
        }
        faceExpressionCoroutine = StartCoroutine(TemporaryFaceRoutine(tempTexture, duration));
    }

    private IEnumerator TemporaryFaceRoutine(Texture2D tempTexture, float duration)
    {
        SetFaceExpression(tempTexture);
        yield return new WaitForSeconds(duration);
        SetFaceExpression(isCharging && happyChargingFaceTexture != null ? happyChargingFaceTexture : normalFaceTexture);
        faceExpressionCoroutine = null;
    }

    public void SetFaceExpression(Texture2D faceTex)
    {
        if (faceRenderer != null && faceTex != null)
        {
            faceRenderer.material.mainTexture = faceTex;
        }
    }

    public float CurrentSpeedKmH => Mathf.Abs(currentSpeed) * 3.6f;
}

