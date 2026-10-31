using UnityEngine;
using UnityEngine.EventSystems;

public class MobileTouchControls : MonoBehaviour
{
    public static MobileTouchControls Instance { get; private set; }

    private float touchThrottle = 0f;
    private float touchSteer = 0f;

    private bool isGasPressed = false;
    private bool isBrakePressed = false;
    private bool isLeftPressed = false;
    private bool isRightPressed = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        // 조향 계산
        if (isLeftPressed && !isRightPressed) touchSteer = -1f;
        else if (isRightPressed && !isLeftPressed) touchSteer = 1f;
        else touchSteer = 0f;

        // 가속/감속 계산
        if (isGasPressed && !isBrakePressed) touchThrottle = 1f;
        else if (isBrakePressed && !isGasPressed) touchThrottle = -1f;
        else touchThrottle = 0f;

        if (BusController.Instance != null)
        {
            BusController.Instance.SetTouchThrottle(touchThrottle);
            BusController.Instance.SetTouchSteer(touchSteer);
        }
    }

    public void OnGasDown() => isGasPressed = true;
    public void OnGasUp() => isGasPressed = false;

    public void OnBrakeDown() => isBrakePressed = true;
    public void OnBrakeUp() => isBrakePressed = false;

    public void OnLeftDown() => isLeftPressed = true;
    public void OnLeftUp() => isLeftPressed = false;

    public void OnRightDown() => isRightPressed = true;
    public void OnRightUp() => isRightPressed = false;

    public void OnHornClicked()
    {
        if (BusController.Instance != null)
        {
            BusController.Instance.HonkHorn();
        }
    }
}

