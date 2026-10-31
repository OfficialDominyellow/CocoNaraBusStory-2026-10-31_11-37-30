using UnityEngine;

public static class AudioGenerator
{
    private const int SampleRate = 44100;

    /// <summary>
    /// 버스 경적 소리 ("빵빵!"): 두 번 연속 울리는 경쾌한 클랙슨 톤
    /// </summary>
    public static AudioClip CreateHornClip()
    {
        float duration = 0.65f;
        int totalSamples = (int)(SampleRate * duration);
        float[] samples = new float[totalSamples];

        float f1 = 440f; // A4
        float f2 = 554.37f; // C#5

        int burst1End = (int)(SampleRate * 0.22f);
        int gapEnd = (int)(SampleRate * 0.30f);
        int burst2End = (int)(SampleRate * 0.58f);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float val = 0f;

            if (i < burst1End)
            {
                float env = Mathf.Sin((float)i / burst1End * Mathf.PI);
                val = (Mathf.Sin(2 * Mathf.PI * f1 * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * f2 * t) * 0.5f) * env;
            }
            else if (i >= gapEnd && i < burst2End)
            {
                int sampleInBurst = i - gapEnd;
                int burstLen = burst2End - gapEnd;
                float env = Mathf.Sin((float)sampleInBurst / burstLen * Mathf.PI);
                val = (Mathf.Sin(2 * Mathf.PI * f1 * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * f2 * t) * 0.5f) * env;
            }

            samples[i] = Mathf.Clamp(val * 0.8f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Horn_BBangBBang", totalSamples, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// 승객 탑승 효과음: 경쾌하고 맑은 상승 차임벨 ("딩동댕~")
    /// </summary>
    public static AudioClip CreateBoardingClip()
    {
        float duration = 0.7f;
        int totalSamples = (int)(SampleRate * duration);
        float[] samples = new float[totalSamples];

        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6
        float noteDuration = duration / notes.Length;
        int noteSamples = (int)(SampleRate * noteDuration);

        for (int i = 0; i < totalSamples; i++)
        {
            int noteIndex = Mathf.Clamp(i / noteSamples, 0, notes.Length - 1);
            float freq = notes[noteIndex];
            int noteSampleIndex = i % noteSamples;
            float noteT = (float)noteSampleIndex / noteSamples;

            // 지수 감쇄 엔벨로프
            float env = Mathf.Exp(-3.5f * noteT);
            float t = (float)i / SampleRate;

            // 배음 포함 벨 사운드
            float val = Mathf.Sin(2 * Mathf.PI * freq * t) * 0.6f +
                        Mathf.Sin(2 * Mathf.PI * freq * 2f * t) * 0.25f +
                        Mathf.Sin(2 * Mathf.PI * freq * 3f * t) * 0.15f;

            samples[i] = Mathf.Clamp(val * env * 0.75f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Boarding_Chime", totalSamples, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// 미션 완료 및 하차 팡파레: 신나는 팡파레 축하음
    /// </summary>
    public static AudioClip CreateFanfareClip()
    {
        float duration = 1.2f;
        int totalSamples = (int)(SampleRate * duration);
        float[] samples = new float[totalSamples];

        // 팡파레 멜로디 (도-미-솔-도!)
        float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f, 1046.50f };
        float[] durations = { 0.15f, 0.15f, 0.15f, 0.35f, 0.40f };

        int currentSample = 0;
        for (int n = 0; n < freqs.Length && currentSample < totalSamples; n++)
        {
            float freq = freqs[n];
            int noteLen = (int)(SampleRate * durations[n]);
            for (int i = 0; i < noteLen && currentSample < totalSamples; i++)
            {
                float t = (float)currentSample / SampleRate;
                float progress = (float)i / noteLen;
                float env = 1f - Mathf.Pow(progress, 1.5f);

                // 브라스 스타일 하모닉스
                float val = Mathf.Sin(2 * Mathf.PI * freq * t) * 0.5f +
                            Mathf.Sin(2 * Mathf.PI * freq * 2f * t) * 0.3f +
                            Mathf.Sin(2 * Mathf.PI * freq * 3f * t) * 0.15f;

                samples[currentSample] = Mathf.Clamp(val * env * 0.7f, -1f, 1f);
                currentSample++;
            }
        }

        AudioClip clip = AudioClip.Create("Mission_Fanfare", totalSamples, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// 전기차 충전 효과음: 미래지향적인 충전 상승음과 스파크 비트
    /// </summary>
    public static AudioClip CreateEVChargeClip()
    {
        float duration = 1.0f;
        int totalSamples = (int)(SampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / totalSamples;

            // 주파수가 상승하는 신스 사운드 (220Hz -> 880Hz)
            float freq = Mathf.Lerp(220f, 880f, progress);
            float env = Mathf.Sin(progress * Mathf.PI);

            float val = Mathf.Sin(2 * Mathf.PI * freq * t) * 0.5f +
                        Mathf.Sin(2 * Mathf.PI * (freq * 1.5f) * t) * 0.3f;

            samples[i] = Mathf.Clamp(val * env * 0.6f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("EV_Charging", totalSamples, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// 퀴즈 정답 효과음: 반짝이는 별 효과음 ("띵동! ⭐")
    /// </summary>
    public static AudioClip CreateCorrectClip()
    {
        float duration = 0.5f;
        int totalSamples = (int)(SampleRate * duration);
        float[] samples = new float[totalSamples];

        float[] freqs = { 880f, 1174.66f }; // A5 -> D6
        int halfSamples = totalSamples / 2;

        for (int i = 0; i < totalSamples; i++)
        {
            float freq = (i < halfSamples) ? freqs[0] : freqs[1];
            int noteI = (i < halfSamples) ? i : i - halfSamples;
            float noteT = (float)noteI / halfSamples;
            float env = Mathf.Exp(-4f * noteT);
            float t = (float)i / SampleRate;

            float val = Mathf.Sin(2 * Mathf.PI * freq * t);
            samples[i] = Mathf.Clamp(val * env * 0.7f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Quiz_Correct", totalSamples, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

