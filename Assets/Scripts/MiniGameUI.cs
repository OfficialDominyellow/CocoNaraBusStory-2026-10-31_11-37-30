using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MiniGameUI : MonoBehaviour
{
    public static MiniGameUI Instance { get; private set; }
    public static bool IsActive { get; private set; } = false;

    [Header("UI 요소")]
    public GameObject modalRoot;
    public TextMeshProUGUI titleTMP;
    public TextMeshProUGUI questionTMP;
    public List<Button> cardButtons = new List<Button>();
    public List<TextMeshProUGUI> cardTexts = new List<TextMeshProUGUI>();
    public TextMeshProUGUI celebrationTMP;

    [Header("축하 이펙트 & 사운드")]
    public ParticleSystem celebrationUIParticles;
    public AudioSource audioSource;

    private Action onCompleteCallback;
    private int correctIndex = 0;
    private bool isAnswering = false;

    private AudioClip correctClip;
    private AudioClip fanfareClip;

    private void Awake()
    {
        Instance = this;
        if (modalRoot != null)
        {
            modalRoot.SetActive(false);
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        correctClip = AudioGenerator.CreateCorrectClip();
        fanfareClip = AudioGenerator.CreateFanfareClip();
    }

    /// <summary>
    /// 목적지 도착 시 3지선다 카드 맞추기 퀴즈 팝업 실행
    /// </summary>
    public void ShowQuiz(string destinationName, Action onComplete)
    {
        onCompleteCallback = onComplete;
        IsActive = true;
        isAnswering = false;

        if (modalRoot != null)
        {
            modalRoot.SetActive(true);
        }

        if (celebrationTMP != null)
        {
            celebrationTMP.gameObject.SetActive(false);
        }

        if (titleTMP != null)
        {
            titleTMP.text = "⭐ 신나는 도착 퀴즈! ⭐";
        }

        if (questionTMP != null)
        {
            questionTMP.text = $"<color=#FFD700>'{destinationName}'</color>에 도착했어요!\n아래에서 알맞은 간판을 찾아 터치해 볼까요?";
        }

        // 보기 3개 구성 (1개는 정답, 2개는 매력적인 오답)
        List<string> options = GenerateQuizOptions(destinationName);
        correctIndex = options.IndexOf(destinationName);

        for (int i = 0; i < cardButtons.Count; i++)
        {
            int index = i;
            if (index < options.Count)
            {
                cardButtons[index].gameObject.SetActive(true);
                if (cardTexts.Count > index && cardTexts[index] != null)
                {
                    cardTexts[index].text = options[index];
                }

                cardButtons[index].onClick.RemoveAllListeners();
                cardButtons[index].onClick.AddListener(() => OnCardClicked(index));
            }
            else
            {
                cardButtons[index].gameObject.SetActive(false);
            }
        }
    }

    private List<string> GenerateQuizOptions(string correctOption)
    {
        List<string> pool = new List<string>
        {
            "아이파크캐슬",
            "코코어린이집",
            "나라유치원",
            "왕십리 삼성아파트",
            "하남 풍산 아이파크",
            "에버랜드",
            "롯데월드",
            "하남 스타필드",
            "글빛공원",
            "인천공항",
            "전기차 충전소"
        };

        pool.Remove(correctOption);
        // 무작위 셔플
        for (int i = 0; i < pool.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, pool.Count);
            string tmp = pool[i];
            pool[i] = pool[r];
            pool[r] = tmp;
        }

        List<string> result = new List<string>
        {
            correctOption,
            pool[0],
            pool[1]
        };

        // 정답 위치 셔플
        for (int i = 0; i < result.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, result.Count);
            string tmp = result[i];
            result[i] = result[r];
            result[r] = tmp;
        }

        return result;
    }

    private void OnCardClicked(int clickedIndex)
    {
        if (isAnswering) return;

        if (clickedIndex == correctIndex)
        {
            isAnswering = true;
            StartCoroutine(CelebrationRoutine());
        }
        else
        {
            // 오답 시 힌트 연출 (유아 배려: 혼내지 않고 격려)
            if (questionTMP != null)
            {
                questionTMP.text = "우와, 아쉬워요! 다른 카드를 한번 더 골라볼까요? 😊";
            }
        }
    }

    private IEnumerator CelebrationRoutine()
    {
        // 정답 효과음 및 팡파레 재생
        if (audioSource != null)
        {
            if (correctClip != null) audioSource.PlayOneShot(correctClip, 0.9f);
            if (fanfareClip != null) audioSource.PlayOneShot(fanfareClip, 0.85f);
        }

        // 별 파티클 연출
        if (celebrationUIParticles != null)
        {
            celebrationUIParticles.Play();
        }

        // 축하 문구 표시
        if (celebrationTMP != null)
        {
            celebrationTMP.gameObject.SetActive(true);
            celebrationTMP.text = "🎉 정답이에요! 참 잘했어요! ⭐⭐⭐";
        }

        // 버스 표정도 신나게 윙크
        if (BusController.Instance != null && BusController.Instance.winkFaceTexture != null)
        {
            BusController.Instance.TriggerTemporaryFace(BusController.Instance.winkFaceTexture, 2.0f);
        }

        yield return new WaitForSeconds(2.0f);

        CloseModal();
        onCompleteCallback?.Invoke();
    }

    public void CloseModal()
    {
        if (modalRoot != null)
        {
            modalRoot.SetActive(false);
        }
        IsActive = false;
        isAnswering = false;
    }
}

