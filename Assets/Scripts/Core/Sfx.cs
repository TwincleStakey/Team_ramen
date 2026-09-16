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

    /// <summary>
    /// 효과음 동시 재생 수. 반짝임처럼 겹쳐 나는 소리를 위해 여럿 둔다.
    /// 대사 톤·발소리·컷신 소리도 이 자리를 같이 쓴다(<see cref="PlayClip"/>).
    /// </summary>
    private const int Voices = 12;

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

    // ── 사용자 볼륨 ──
    //
    // 위의 표는 **연출이 정한 크기**다(우주 컷 동안 가게 음악을 0.06 으로 줄이는 식).
    // 여기 두 배율은 **사용자가 정하는 크기**다. 둘을 곱해서 실제 크기가 나온다.
    //
    // 눈금 0~10 으로 끊는다. 화면 필터(ScreenGrade.MaxStep)와 같은 눈금이라 설정창이
    // 세 줄을 같은 위젯 하나로 그린다. 기본은 10(=1.0)이라 **아무것도 안 건드린 상태의
    // 소리는 예전과 똑같다.**
    //
    // 어느 배율을 타는지는 **이름 앞머리**로 가른다. bgm_ 으로 시작하면 배경음(가게·타이틀·
    // 우주·크레딧), 나머지는 전부 효과음이다. 거리·주방 앰비언스(amb_)도 효과음 쪽이다 —
    // 음악이 아니라 현장음이라 효과음을 줄일 때 같이 줄어야 맞는다.
    public const int MaxVolumeStep = 10;

    private const string BgmPrefKey = "audio.bgm";
    private const string SfxPrefKey = "audio.sfx";
    private const string BgmPrefix = "bgm_";

    private static int bgmStep = MaxVolumeStep;
    private static int sfxStep = MaxVolumeStep;
    private static bool volumesLoaded;

    /// <summary>배경음 눈금(0~10). 바꾸면 돌고 있는 루프에 바로 먹는다.</summary>
    public static int BgmStep
    {
        get { LoadVolumes(); return bgmStep; }
        set
        {
            LoadVolumes();
            int clamped = Mathf.Clamp(value, 0, MaxVolumeStep);
            if (clamped == bgmStep) return;

            bgmStep = clamped;
            PlayerPrefs.SetInt(BgmPrefKey, bgmStep);
            PlayerPrefs.Save();
            ApplyLoopVolumes();
        }
    }

    /// <summary>효과음 눈금(0~10). 다음에 나는 소리부터 먹는다.</summary>
    public static int SfxStep
    {
        get { LoadVolumes(); return sfxStep; }
        set
        {
            LoadVolumes();
            int clamped = Mathf.Clamp(value, 0, MaxVolumeStep);
            if (clamped == sfxStep) return;

            sfxStep = clamped;
            PlayerPrefs.SetInt(SfxPrefKey, sfxStep);
            PlayerPrefs.Save();
            ApplyLoopVolumes();      // 앰비언스 루프도 효과음 쪽이라 같이 다시 먹인다
        }
    }

    private static void LoadVolumes()
    {
        if (volumesLoaded) return;
        volumesLoaded = true;

        bgmStep = Mathf.Clamp(PlayerPrefs.GetInt(BgmPrefKey, MaxVolumeStep), 0, MaxVolumeStep);
        sfxStep = Mathf.Clamp(PlayerPrefs.GetInt(SfxPrefKey, MaxVolumeStep), 0, MaxVolumeStep);
    }

    /// <summary>그 이름이 타는 배율. 연출이 정한 크기에 이걸 곱한다.</summary>
    private static float ScaleFor(string name)
    {
        LoadVolumes();
        bool bgm = !string.IsNullOrEmpty(name) && name.StartsWith(BgmPrefix);
        return (bgm ? bgmStep : sfxStep) / (float)MaxVolumeStep;
    }

    /// <summary>
    /// 돌고 있는 루프에 바뀐 배율을 다시 먹인다.
    ///
    /// 루프는 한 번 켜면 계속 돌므로, 설정을 바꾼 순간 다시 먹이지 않으면 다음에 누가
    /// <see cref="SetLoopVolume"/> 을 부를 때까지 옛 크기로 남는다. 그래서 **부탁받은
    /// 크기**(연출이 정한 값)를 따로 기억해 둔다 — 그게 없으면 이미 배율이 곱해진 값에
    /// 또 곱하게 된다.
    /// </summary>
    private static void ApplyLoopVolumes()
    {
        if (player == null) return;

        foreach (KeyValuePair<string, AudioSource> pair in player.Loops)
        {
            float asked;
            if (!player.Requested.TryGetValue(pair.Key, out asked)) continue;

            player.Fade(pair.Key, pair.Value, asked * ScaleFor(pair.Key), 0.15f, false);
        }
    }

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

        // 볼륨도 다시 읽게 둔다. PlayerPrefs 는 살아 있지만 여기 캐시가 옛 값이면
        // 설정을 바꾸고 Play 를 다시 눌렀을 때 바뀌기 전 크기로 시작한다.
        volumesLoaded = false;
    }

    /// <summary>효과음 한 번. jitter 는 높낮이를 ±얼마나 흔들지(0.08 이면 ±8%).</summary>
    public static void Play(string name, float volume = 1f, float pitch = 1f, float jitter = 0f)
    {
        // 배율은 PlayClip 이 한 번만 곱한다. 여기서도 곱하면 두 번 걸린다.
        PlayClip(Clip(name), volume, pitch + Random.Range(-jitter, jitter));
    }

    /// <summary>
    /// 이미 손에 든 클립을 한 번 낸다. 파형을 코드로 만들어 두는 쪽(대사 톤·발소리·컷신)이 쓴다.
    ///
    /// 자기 AudioSource 에 직접 내면 안 된다 — <c>pitch</c> 는 그 소스에서 아직 울리는 중인
    /// 소리에까지 같이 걸려서 앞 소리의 음까지 틀어 놓는다. 대사 톤은 40ms 마다 60ms 짜리를
    /// 쏘므로 늘 겹친다. 여기로 오면 소리마다 다른 자리를 받는다.
    /// </summary>
    public static void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        Player p = Get();
        if (p == null) return;

        // 사용자 효과음 배율. 여기가 효과음이 나는 유일한 길목이라 한 군데만 곱하면 된다
        // (Play 도 결국 여기로 온다). 0 이면 아예 자리를 안 쓴다.
        float scaled = volume * SfxScale;
        if (scaled <= 0f) return;

        AudioSource voice = p.NextVoice();
        voice.pitch = pitch;
        voice.PlayOneShot(clip, scaled);
    }

    /// <summary>효과음 배율(0~1). 이름으로 가르지 않는 자리(PlayClip)가 쓴다.</summary>
    private static float SfxScale
    {
        get { LoadVolumes(); return sfxStep / (float)MaxVolumeStep; }
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

        // 연출이 부탁한 크기를 그대로 기억해 둔다. 설정이 바뀌면 이 값에 새 배율을 곱해
        // 다시 먹인다 — 이미 곱해진 값만 들고 있으면 곱하기가 겹쳐서 소리가 사그라든다.
        p.Requested[name] = volume;
        p.Fade(name, source, volume * ScaleFor(name), fadeSeconds, false);
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

        p.Requested[name] = volume;
        p.Fade(name, source, volume * ScaleFor(name), fadeSeconds, false);
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

        /// <summary>연출이 부탁한 크기. 사용자 배율을 곱하기 **전** 값이다.</summary>
        public readonly Dictionary<string, float> Requested = new Dictionary<string, float>();

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

        /// <summary>
        /// 효과음 자리를 준다. 노는 자리를 먼저 찾고, 다 차 있으면 그냥 돌려가며 준다.
        ///
        /// 한 자리에 겹쳐 내면 뒤 소리가 앞 소리의 pitch 까지 바꾼다. 3.5초짜리 우주 소리가
        /// 도는 중에 반짝임이 여럿 나면 우주 소리가 중간에 음이 틀어졌다.
        /// </summary>
        public AudioSource NextVoice()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                AudioSource candidate = voices[(next + i) % voices.Length];
                if (candidate.isPlaying) continue;

                next = (next + i + 1) % voices.Length;
                return candidate;
            }

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
                Requested.Remove(name);
                targets.Remove(name);
                Destroy(source);
            }

            fades.Remove(name);
        }
    }
}
