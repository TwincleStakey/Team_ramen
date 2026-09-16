using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대사가 한 글자씩 찍힐 때마다 나는 짧은 톤(기획서 10.2).
///
/// 말투마다 wav를 두면 열다섯 개가 된다. 대신 사인파 한 토막을 코드로 만들어 두고
/// 재생할 때 pitch만 바꾼다. 소리의 성격은 높낮이와 빠르기, 그리고 흔들림으로 낸다.
///
/// 말투(personaId)는 읽기만 한다. 대사 생성은 B 코드가 하고 이쪽은 소리만 낸다.
/// </summary>
public class DialogueBlip : MonoBehaviour
{
    /// <summary>말투 하나의 목소리 성격.</summary>
    private struct Voice
    {
        /// <summary>기본 높낮이. 1이 원음이고 낮을수록 굵다.</summary>
        public float Pitch;

        /// <summary>글자 사이 간격(초). 기획서가 정한 30~50ms 범위 안에서 고른다.</summary>
        public float Interval;

        /// <summary>글자마다 높낮이를 얼마나 흔들지. 0이면 기계처럼 고르게 난다.</summary>
        public float Jitter;

        public Voice(float pitch, float interval, float jitter)
        {
            Pitch = pitch;
            Interval = interval;
            Jitter = jitter;
        }
    }

    // 말투 15종. 기획서 10.2의 예시(군인 낮고 빠름, 아이 높고 통통, 할머니 느림,
    // 유튜버 높고 빠름, 오타쿠 낮고 불규칙)를 기준으로 나머지를 채웠다.
    private static readonly Dictionary<string, Voice> Voices = new Dictionary<string, Voice>
    {
        { "Polite",     new Voice(1.00f, 0.040f, 0.03f) },
        { "Formal",     new Voice(0.92f, 0.045f, 0.02f) },
        { "Gyeongsang", new Voice(0.96f, 0.034f, 0.06f) },
        { "Chungcheong",new Voice(0.90f, 0.050f, 0.04f) },   // 느릿하게
        { "Jeolla",     new Voice(0.98f, 0.038f, 0.06f) },
        { "Otaku",      new Voice(0.84f, 0.036f, 0.12f) },   // 낮고 불규칙
        { "Military",   new Voice(0.72f, 0.030f, 0.02f) },   // 낮고 빠름
        { "Sageuk",     new Voice(0.80f, 0.048f, 0.03f) },
        { "Child",      new Voice(1.35f, 0.033f, 0.10f) },   // 높고 통통
        { "Grandma",    new Voice(1.05f, 0.050f, 0.05f) },   // 느림
        { "Grandpa",    new Voice(0.76f, 0.050f, 0.05f) },
        { "Youtuber",   new Voice(1.25f, 0.030f, 0.08f) },   // 높고 빠름
        { "Gourmet",    new Voice(0.94f, 0.042f, 0.03f) },
        { "Emotional",  new Voice(1.10f, 0.044f, 0.07f) },
        { "Joker",      new Voice(1.15f, 0.032f, 0.14f) },   // 촐랑거리게
    };

    private static readonly Voice Fallback = new Voice(1f, 0.040f, 0.04f);

    /// <summary>마침표·물음표에서 쉬는 시간. 말이 끊기는 자리를 귀로 알 수 있게 한다.</summary>
    public const float SentencePause = 0.12f;

    /// <summary>쉼표에서 쉬는 시간. 마침표보다 짧다.</summary>
    public const float CommaPause = 0.06f;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.25f;

    private AudioClip tone;
    private Voice voice = Fallback;

    private void Awake()
    {
        // 음원 파일이 있으면 그것을 쓰고, 없으면 예전처럼 사인파를 만든다.
        tone = Sfx.ClipOr("sfx_voice_blip", BuildTone);
    }

    /// <summary>
    /// 짧은 사인파 한 토막을 만든다.
    ///
    /// 앞뒤를 서서히 키우고 줄인다(페이드). 그냥 자르면 파형이 툭 끊겨
    /// "틱" 하는 잡음이 같이 난다. 픽셀 게임의 말소리답게 아주 짧게 잡았다.
    /// </summary>
    private static AudioClip BuildTone()
    {
        const int rate = 44100;
        const float seconds = 0.055f;
        const float frequency = 440f;

        int count = Mathf.RoundToInt(rate * seconds);
        var samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float wave = Mathf.Sin(2f * Mathf.PI * frequency * t);

            // 앞 10%는 키우고 뒤 60%는 줄인다. 뒤를 길게 줄여야 여운이 남는다.
            float progress = (float)i / count;
            float envelope = progress < 0.1f
                ? progress / 0.1f
                : Mathf.Clamp01((1f - progress) / 0.6f);

            samples[i] = wave * envelope;
        }

        var clip = AudioClip.Create("DialogueBlip", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>손님이 바뀔 때 부른다. 그 손님의 목소리로 맞춰 둔다.</summary>
    public void SetPersona(string personaId)
    {
        Voice found;
        voice = !string.IsNullOrEmpty(personaId) && Voices.TryGetValue(personaId, out found)
            ? found
            : Fallback;
    }

    /// <summary>글자 하나가 찍힐 때 부른다.</summary>
    public void PlayTone()
    {
        Sfx.PlayClip(tone, volume, voice.Pitch + Random.Range(-voice.Jitter, voice.Jitter));
    }

    /// <summary>이 손님이 글자 하나를 찍는 데 걸리는 시간.</summary>
    public float Interval
    {
        get { return voice.Interval; }
    }

    /// <summary>
    /// 소리를 내지 않는 글자인가. 공백과 문장부호에서는 톤을 쉰다.
    /// 전부 소리를 내면 "다다다다" 하고 말이 아니라 기계음처럼 들린다.
    /// </summary>
    public static bool IsSilent(char c)
    {
        return char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c);
    }

    /// <summary>이 글자 뒤에 얼마나 쉴지. 문장이 끝나는 자리에서 숨을 돌린다.</summary>
    public static float PauseAfter(char c)
    {
        if (c == '.' || c == '!' || c == '?') return SentencePause;
        if (c == ',') return CommaPause;
        return 0f;
    }
}
