using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    [Header("미션 및 가이드 UI")]
    public TextMeshProUGUI missionTextTMP;
    public GameObject toastPanel;
    public TextMeshProUGUI toastTextTMP;

    [Header("배터리 및 속도계")]
    public Image batteryFillImage;
    public TextMeshProUGUI batteryTextTMP;
    public TextMeshProUGUI speedTextTMP;
    public TextMeshProUGUI passengerCountTMP;

    [Header("경적 및 조작 버튼")]
    public Button hornButton;

    private Coroutine toastCoroutine;

    private void Awake()
    {
        Instance = this;
        if (toastPanel != null)
        {
            toastPanel.SetActive(false);
        }

        if (hornButton != null)
        {
            hornButton.onClick.AddListener(() =>
            {
                if (BusController.Instance != null)
                {
                    BusController.Instance.HonkHorn();
                }
            });
        }
    }

    private void Update()
    {
        UpdateHUDValues();
    }

    private void UpdateHUDValues()
    {
        if (BusController.Instance == null) return;

        // 배터리 게이지 업데이트
        float batteryPct = BusController.Instance.currentBattery / BusController.Instance.maxBattery;
        if (batteryFillImage != null)
        {
            batteryFillImage.fillAmount = batteryPct;
            if (batteryPct > 0.4f)
                batteryFillImage.color = new Color(0.2f, 0.85f, 0.3f); // 녹색
            else if (batteryPct > 0.15f)
                batteryFillImage.color = new Color(1.0f, 0.8f, 0.1f); // 노란색
            else
                batteryFillImage.color = new Color(1.0f, 0.25f, 0.25f); // 빨간색
        }

        if (batteryTextTMP != null)
        {
            string chargeIcon = BusController.Instance.isCharging ? " ⚡충전중!" : "";
            batteryTextTMP.text = $"{Mathf.CeilToInt(batteryPct * 100f)}%{chargeIcon}";
        }

        // 속도계 업데이트
        if (speedTextTMP != null)
        {
            speedTextTMP.text = $"{Mathf.RoundToInt(BusController.Instance.CurrentSpeedKmH)} km/h";
        }

        // 탑승 승객 수
        if (passengerCountTMP != null && PassengerManager.Instance != null)
        {
            passengerCountTMP.text = $"승객: {PassengerManager.Instance.boardedPassengers.Count}명 👨‍👩‍👧‍👦";
        }
    }

    public void UpdateMissionDisplay(string newMission)
    {
        if (missionTextTMP != null)
        {
            missionTextTMP.text = newMission;
        }
    }

    public void ShowToastNotification(string message, float duration = 2.8f)
    {
        if (toastCoroutine != null)
        {
            StopCoroutine(toastCoroutine);
        }
        toastCoroutine = StartCoroutine(ToastRoutine(message, duration));
    }

    private IEnumerator ToastRoutine(string message, float duration)
    {
        if (toastPanel != null && toastTextTMP != null)
        {
            toastTextTMP.text = message;
            toastPanel.SetActive(true);
            yield return new WaitForSeconds(duration);
            toastPanel.SetActive(false);
        }
        toastCoroutine = null;
    }
}

