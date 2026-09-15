using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 소리를 파일 이름으로 낸다. <c>Sfx.Play("sfx_cook_drop")</c> 처럼 쓴다.
///
/// 파일은 <c>Assets/Resources/Audio/</c> 에 둔다. 씬에는 아무것도 꽂지 않는다 —
/// 빌더가 씬을 통째로 다시 만들어도 배선이 안 끊기고, 진짜 음원이 오면 같은 이름으로
/// 덮어쓰기만 하면 된다. 처음 부를 때 숨은 오브젝트 하나를 만들어 AudioSource 를 물린다.
///
/// 효과음은 <see cref="Play"/>, 배경음·앰비언스는 <see cref="Loop"/>·<see cref="Stop"/>·
/// <see cref="SetLoopVolume"/> 로 다룬다. 루프는 이름 하나에 AudioSource 하나다.
/// 페이드는 unscaled 시간을 쓴다 — 확인창이 timeScale 을 0 으로 내려도 소리는 줄어야 한다.
/// </summary>
public static class Sfx
{
    private const string Folder = "Audio/";

    /// <summary>효과음 동시 재생 수. 반짝임처럼 겹쳐 나는 소리를 위해 여럿 둔다.</summary>
    private const int Voices = 8;

    // ── 배경음 크기 표 ──
    // 켜는 곳(GameManager)과 줄이는 곳(OrderScreenUI·EatingCutscene)이 서로 다른 파일이라
    // 같은 값을 여기서 본다. 효과음 크기는 부르는 자리에서 바로 적는다.
    public const float TitleBgm = 0.4f;
    public const float ShopBgm = 0.35f;
    public const float ShopBgmDucked = 0.06f;           // 우주 컷 동안
    public const float StreetAmbience = 0.2f;
    public const float KitchenAmbience = 0.3f;          // 조리 화면이 보일 때
    public const float KitchenAmbienceCovered = 0.08f;  // 주문 화면이 덮었을 때
    public const float CosmosBgm = 0.5f;

    private static Player player;
    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private static readonly HashSet<string> missing = new HashSet<string>();

    /// <summary>Play 모드에 다시 들어갈 때 정적 상태를 비운다(도메인 리로드를 끈 설정 대비).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        player = null;
        clips.Clear();
        missing.Clear();
    }

    /// <summary>효과음 한 번. jitter 는 높낮이를 ±얼마나 흔들지(0.08 이면 ±8%).</summary>
    public static void Play(string name, float volume = 1f, float pitch = 1f, float jitter = 0f)
    {
        AudioClip clip = Clip(name);
        if (clip == null) return;

        Player p = Get();
        if (p == null) return;

        AudioSource voice = p.NextVoice();
        voice.pitch = pitch + Random.Range(-jitter, jitter);
        voice.PlayOneShot(clip, volume);
    }

    /// <summary>루프를 켠다. 이미 돌고 있으면 크기만 맞춘다.</summary>
    public static void Loop(string name, float volume = 1f, float fadeSeconds = 0.5f)
    {
        Player p = Get();
        if (p == null) return;

        AudioSource source;
        if (!p.Loops.TryGetValue(name, out source))
        {
            AudioClip clip = Clip(name);
            if (clip == null) return;

            source = p.gameObject.AddComponent<AudioSource>();
            Configure(source);
            source.loop = true;
            source.clip = clip;
            source.volume = 0f;
            source.Play();
            p.Loops[name] = source;
        }

        p.Fade(name, source, volume, fadeSeconds, false);
    }

    /// <summary>루프를 줄여서 끈다. 안 돌고 있으면 아무 일도 없다.</summary>
    public static void Stop(string name, float fadeSeconds = 0.5f)
    {
        Player p = Get();
        AudioSource source;
        if (p == null || !p.Loops.TryGetValue(name, out source)) return;

        p.Fade(name, source, 0f, fadeSeconds, true);
    }

    /// <summary>돌고 있는 루프의 크기를 바꾼다. 주문 화면이 덮일 때 조리 소리를 줄이는 데 쓴다.</summary>
    public static void SetLoopVolume(string name, float volume, float fadeSeconds = 0.5f)
    {
        Player p = Get();
        AudioSource source;
        if (p == null || !p.Loops.TryGetValue(name, out source)) return;

        p.Fade(name, source, volume, fadeSeconds, false);
    }

    /// <summary>
    /// 파일이 있으면 그것을, 없으면 build 로 만든 것을 준다.
    /// 파형을 코드로 만들던 컴포넌트(DialogueBlip 등)가 예전 방식을 대비책으로 남겨 두는 데 쓴다.
    /// </summary>
    public static AudioClip ClipOr(string name, System.Func<AudioClip> build)
    {
        AudioClip clip = Clip(name);
        return clip != null ? clip : build();
    }

    /// <summary>파일을 찾는다. 없으면 null 을 주고 경고를 한 번만 남긴다.</summary>
    public static AudioClip Clip(string name)
    {
        AudioClip clip;
        if (clips.TryGetValue(name, out clip)) return clip;

        clip = Resources.Load<AudioClip>(Folder + name);
        if (clip != null) clips[name] = clip;
        else if (missing.Add(name)) Debug.LogWarning("[Sfx] 음원이 없습니다: Resources/" + Folder + name);

        return clip;
    }

    private static Player Get()
    {
        if (player != null) return player;
        if (!Application.isPlaying) return null;

        var go = new GameObject("[Sfx]");
        go.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(go);
        player = go.AddComponent<Player>();
        return player;
    }

    private static void Configure(AudioSource source)
    {
        source.playOnAwake = false;
        source.spatialBlend = 0f;           // UI 소리라 거리와 무관해야 한다
        source.ignoreListenerPause = true;  // 게임이 멈춰도 UI 소리는 나야 한다
    }

    /// <summary>실제로 소리를 내는 숨은 컴포넌트. 바깥에서는 Sfx 의 정적 함수만 쓴다.</summary>
    private class Player : MonoBehaviour
    {
        public readonly Dictionary<string, AudioSource> Loops = new Dictionary<string, AudioSource>();

        private readonly Dictionary<string, Coroutine> fades = new Dictionary<string, Coroutine>();
        private readonly Dictionary<string, float> targets = new Dictionary<string, float>();
        private AudioSource[] voices;
        private int next;

        private void Awake()
        {
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                Configure(voices[i]);
            }
        }

        /// <summary>효과음 자리를 돌려가며 준다. 한 자리에 겹쳐 내면 pitch 가 서로 밟힌다.</summary>
        public AudioSource NextVoice()
        {
            AudioSource voice = voices[next];
            next = (next + 1) % voices.Length;
            return voice;
        }

        /// <summary>
        /// 이미 같은 목표로 가고 있으면 아무것도 안 한다. 면 털기처럼 매 프레임 Loop 를
        /// 불러도 코루틴이 프레임마다 새로 생기지 않게 한다. 끄는 것은 목표 -1 로 구분한다.
        /// </summary>
        public void Fade(string name, AudioSource source, float target, float seconds, bool stopAfter)
        {
            float key = stopAfter ? -1f : target;
            float current;
            if (targets.TryGetValue(name, out current) && Mathf.Approximately(current, key)) return;
            targets[name] = key;

            Coroutine running;
            if (fades.TryGetValue(name, out running) && running != null) StopCoroutine(running);

            fades[name] = StartCoroutine(FadeRoutine(name, source, target, seconds, stopAfter));
        }

        private IEnumerator FadeRoutine(string name, AudioSource source, float target, float seconds, bool stopAfter)
        {
            float from = source.volume;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            source.volume = target;

            if (stopAfter)
            {
                source.Stop();
                Loops.Remove(name);
                targets.Remove(name);
                Destroy(source);
            }

            fades.Remove(name);
        }
    }
}
