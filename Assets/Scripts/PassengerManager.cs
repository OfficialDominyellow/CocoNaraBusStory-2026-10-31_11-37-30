using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PassengerManager : MonoBehaviour
{
    public static PassengerManager Instance { get; private set; }

    [Header("승객 및 미션 관리")]
    public List<Passenger> boardedPassengers = new List<Passenger>();
    public List<Passenger> allWorldPassengers = new List<Passenger>();
    public string currentMissionText = "아이파크캐슬에서 선율이와 태율이를 태워주세요! 🚌";

    [Header("사운드")]
    public AudioSource audioSource;
    private AudioClip boardingClip;
    private AudioClip fanfareClip;

    private void Awake()
    {
        Instance = this;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        boardingClip = AudioGenerator.CreateBoardingClip();
        fanfareClip = AudioGenerator.CreateFanfareClip();
    }

    private void Start()
    {
        // 씬 내 모든 승객 수집
        Passenger[] passengers = FindObjectsByType<Passenger>(FindObjectsSortMode.None);
        allWorldPassengers.Clear();
        allWorldPassengers.AddRange(passengers);

        UpdateMissionText();
    }

    /// <summary>
    /// 승객 탑승 시도
    /// </summary>
    public void TryBoardPassenger(Passenger passenger)
    {
        if (passenger == null || passenger.isBoarded) return;

        passenger.OnBoarded();
        boardedPassengers.Add(passenger);

        // 탑승 사운드 재생
        if (audioSource != null && boardingClip != null)
        {
            audioSource.PlayOneShot(boardingClip, 0.9f);
        }

        // 버스 윙크 표정
        if (BusController.Instance != null && BusController.Instance.winkFaceTexture != null)
        {
            BusController.Instance.TriggerTemporaryFace(BusController.Instance.winkFaceTexture, 1.2f);
        }

        string msg = $"{passenger.passengerName} 탑승 완료! ({boardedPassengers.Count}명 탑승 중)";
        if (GameHUD.Instance != null)
        {
            GameHUD.Instance.ShowToastNotification(msg);
        }

        UpdateMissionText();
    }

    /// <summary>
    /// 랜드마크 도착 시 승객 하차 및 미션 완료 검사
    /// </summary>
    public void OnArrivedAtLandmark(Landmark landmark)
    {
        if (landmark == null) return;

        // 하차할 승객 탐색
        List<Passenger> passengersToDrop = new List<Passenger>();
        foreach (var p in boardedPassengers)
        {
            if (p.destinationLandmarkName == landmark.landmarkName)
            {
                passengersToDrop.Add(p);
            }
        }

        if (passengersToDrop.Count > 0)
        {
            // 하차 처리
            foreach (var p in passengersToDrop)
            {
                boardedPassengers.Remove(p);
                Vector3 dropSpot = landmark.transform.position + landmark.transform.right * 3.5f + Vector3.up * 0.5f;
                p.OnDroppedOff(dropSpot);
            }

            landmark.PlayCelebration();
            if (audioSource != null && fanfareClip != null)
            {
                audioSource.PlayOneShot(fanfareClip, 0.85f);
            }

            string dropMsg = $"{passengersToDrop[0].passengerName} 도착 완료! 고마워요 꼬마버스! 🎉";
            if (GameHUD.Instance != null)
            {
                GameHUD.Instance.ShowToastNotification(dropMsg);
            }

            // 도착 퀴즈 팝업 실행
            if (MiniGameUI.Instance != null)
            {
                MiniGameUI.Instance.ShowQuiz(landmark.landmarkName, () =>
                {
                    UpdateMissionText();
                });
            }
            else
            {
                UpdateMissionText();
            }
        }
        else
        {
            // 단순 방문
            if (GameHUD.Instance != null)
            {
                GameHUD.Instance.ShowToastNotification($"[{landmark.landmarkName}] 정류장에 도착했어요! ✨");
            }
        }
    }

    public void UpdateMissionText()
    {
        if (boardedPassengers.Count > 0)
        {
            Passenger target = boardedPassengers[0];
            currentMissionText = $"{target.passengerName}을(를) <color=#FFD700>[{target.destinationLandmarkName}]</color>에 데려다주세요! 🎒";
        }
        else
        {
            // 정류장에 남아있는 승객 확인
            Passenger remaining = null;
            foreach (var p in allWorldPassengers)
            {
                if (!p.isBoarded && p.gameObject.activeInHierarchy)
                {
                    remaining = p;
                    break;
                }
            }

            if (remaining != null)
            {
                currentMissionText = $"정류장에서 <color=#98FB98>[{remaining.passengerName}]</color>을(를) 태워주세요! 🚌";
            }
            else
            {
                currentMissionText = "모든 가족을 목적지까지 모두 데려다주었어요! 최고예요! 🏆🎉";
            }
        }

        if (GameHUD.Instance != null)
        {
            GameHUD.Instance.UpdateMissionDisplay(currentMissionText);
        }
    }
}

