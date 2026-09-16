using System.Collections;
using UnityEngine;

/// <summary>
/// 먹는 연출에 쓰는 소리. 후루룩과 천둥 두 가지다.
///
/// 프로젝트에 오디오 파일이 하나도 없다. 대사 톤(DialogueBlip)·발소리(Footsteps)와 같이
/// 파형을 코드로 만들어 쓴다.
/// </summary>
public class CutsceneSfx : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float slurpVolume = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float thunderVolume = 0.7f;

    [Range(0f, 1f)]
    [SerializeField] private float cawVolume = 0.45f;

    [Range(0f, 1f)]
    [SerializeField] private float tickVolume = 0.35f;

    /// <summary>후루룩 한 모금과 다음 모금 사이(초).</summary>
    [SerializeField] private float slurpGap = 0.55f;

    private const int Rate = 44100;

    private AudioClip slurp;
    private AudioClip thunder;
    private AudioClip caw;
    private AudioClip tick;
    private Coroutine slurping;

    private void Awake()
    {
        // 음원 파일이 있으면 그것을 쓰고, 없으면 예전처럼 파형을 만든다.
        slurp = Sfx.ClipOr("sfx_cut_slurp", BuildSlurp);
        thunder = Sfx.ClipOr("sfx_cut_thunder", BuildThunder);
        caw = Sfx.ClipOr("sfx_cut_crow", BuildCaw);
        tick = Sfx.ClipOr("sfx_cut_dot", BuildTick);
    }

    /// <summary>seconds 동안 후루룩거린다. 한 모금씩 끊어서 여러 번 낸다.</summary>
    public void Slurp(float seconds)
    {
        if (slurping != null) StopCoroutine(slurping);
        slurping = gameObject.activeInHierarchy ? StartCoroutine(SlurpRoutine(seconds)) : null;
    }

    private IEnumerator SlurpRoutine(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            // 모금마다 높낮이를 조금씩 바꾼다. 똑같으면 녹음을 반복 재생하는 티가 난다.
            Sfx.PlayClip(slurp, slurpVolume, Random.Range(0.92f, 1.10f));

            float wait = slurpGap * Random.Range(0.85f, 1.15f);
            yield return new WaitForSecondsRealtime(wait);
            elapsed += wait;
        }

        slurping = null;
    }

    public void Thunder()
    {
        Sfx.PlayClip(thunder, thunderVolume, Random.Range(0.95f, 1.05f));
    }

    /// <summary>톡. 침묵의 점이 하나 찍힐 때 낸다.</summary>
    public void Tick()
    {
        // 셋이 연달아 찍히므로 높이를 조금씩 올려 준다. 같은 높이로 세 번이면 오류음처럼 들린다.
        Sfx.PlayClip(tick, tickVolume, 1f + 0.09f * ticksSoFar);
        ticksSoFar = (ticksSoFar + 1) % 3;
    }

    private int ticksSoFar;

    /// <summary>
    /// 톡.
    ///
    /// 아주 짧은 나무 두드리는 소리다. 사인파 하나를 8밀리초 만에 끊으면 "틱" 이 되고,
    /// 거기에 한 옥타브 위를 살짝 얹으면 나무결이 생긴다. 길게 끌면 물방울 소리가 된다.
    /// </summary>
    private static AudioClip BuildTick()
    {
        const float seconds = 0.05f;
        int count = Mathf.RoundToInt(Rate * seconds);
        var samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float phase = 2f * Mathf.PI * 900f * i / Rate;

            float body = Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 2f);
            float envelope = Mathf.Pow(1f - t, 5f);

            samples[i] = body * 0.18f * envelope;
        }

        return Finish("Tick", samples);
    }

    /// <summary>까악. 어색한 침묵에 까마귀가 지나갈 때 낸다.</summary>
    public void Caw()
    {
        Sfx.PlayClip(caw, cawVolume, Random.Range(0.94f, 1.06f));
    }

    /// <summary>
    /// 까악.
    ///
    /// 까마귀 소리는 목청이 갈라지는 소리다. 맑은 사인파로는 안 나오고, 톱니처럼 배음이 많은
    /// 파형을 **불규칙하게 떨어** 줘야 쉰 소리가 된다. 여기서는 톱니에 잡음을 섞고
    /// 높이를 위에서 아래로 훑어 내린다 — 까마귀는 울 때 음이 처진다.
    ///
    /// "까-악" 두 마디로 끊는다. 한 번만 내면 새보다 오리에 가깝게 들린다.
    /// </summary>
    private static AudioClip BuildCaw()
    {
        const float seconds = 0.62f;
        int count = Mathf.RoundToInt(Rate * seconds);
        var samples = new float[count];

        // (시작 시각, 길이, 시작 높이, 끝 높이) — 짧게 한 번, 길게 한 번.
        var calls = new[]
        {
            new Vector4(0.00f, 0.16f, 760f, 610f),
            new Vector4(0.26f, 0.30f, 700f, 480f),
        };

        float phase = 0f;

        foreach (var call in calls)
        {
            int from = Mathf.RoundToInt(Rate * call.x);
            int len = Mathf.RoundToInt(Rate * call.y);

            for (int i = 0; i < len && from + i < count; i++)
            {
                float t = (float)i / len;
                float freq = Mathf.Lerp(call.z, call.w, t);

                // 갈라지는 목청. 높이를 빠르게 흔들면 쉰 소리가 된다.
                freq *= 1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 58f * t);

                phase += freq / Rate;
                phase -= Mathf.Floor(phase);

                // 톱니 — 배음이 많아 거칠다.
                float saw = phase * 2f - 1f;
                float noise = Random.value * 2f - 1f;

                // 앞은 확 터지고 뒤는 짧게 잦아든다.
                float envelope = t < 0.07f ? t / 0.07f : Mathf.Pow(1f - (t - 0.07f) / 0.93f, 1.3f);

                samples[from + i] += (saw * 0.75f + noise * 0.25f) * 0.22f * envelope;
            }
        }

        return Finish("Caw", samples);
    }

    /// <summary>
    /// 후루룩.
    ///
    /// 국수를 빨아들이는 소리는 "쉬익" 하는 바람 소리에 가깝다. 잡음을 공명 필터에 통과시키고
    /// 그 공명점을 위로 훑어 올리면 빨아들이는 느낌이 난다. 여기에 잘게 떠는 소리를 얹어
    /// 면이 입술에 스치는 것을 흉내 낸다.
    /// </summary>
    private static AudioClip BuildSlurp()
    {
        const float seconds = 0.42f;
        int count = Mathf.RoundToInt(Rate * seconds);
        var samples = new float[count];

        // 2극 공명기. y[n] = x[n] + 2 r cos(w) y[n-1] - r^2 y[n-2]
        float y1 = 0f, y2 = 0f;
        const float r = 0.985f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;

            // 공명점을 480Hz 에서 1900Hz 까지 훑어 올린다.
            float freq = Mathf.Lerp(480f, 1900f, t * t);
            float w = 2f * Mathf.PI * freq / Rate;

            float noise = Random.value * 2f - 1f;
            float y = noise + 2f * r * Mathf.Cos(w) * y1 - r * r * y2;
            y2 = y1; y1 = y;

            // 면이 스치며 떠는 소리
            float flutter = 1f + 0.35f * Mathf.Sin(2f * Mathf.PI * 34f * t);

            // 앞은 빠르게 커지고 뒤는 길게 잦아든다
            float envelope = t < 0.12f ? t / 0.12f : Mathf.Pow(1f - (t - 0.12f) / 0.88f, 1.6f);

            samples[i] = y * 0.06f * flutter * envelope;
        }

        return Finish("Slurp", samples);
    }

    /// <summary>
    /// 콰강.
    ///
    /// 앞머리에 날카롭게 갈라지는 소리를 두고, 뒤에 낮게 우르릉거리는 꼬리를 길게 붙인다.
    /// 꼬리가 없으면 그냥 "칙" 하는 잡음이 되고, 앞머리가 없으면 그냥 바람 소리가 된다.
    /// </summary>
    private static AudioClip BuildThunder()
    {
        const float seconds = 1.15f;
        int count = Mathf.RoundToInt(Rate * seconds);
        var samples = new float[count];

        float low = 0f;      // 한 극 저역 통과. 우르릉거리는 꼬리를 만든다.
        float crackle = 0f;  // 갈라지는 소리는 조금만 깎는다.

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float noise = Random.value * 2f - 1f;

            low += 0.020f * (noise - low);
            crackle += 0.42f * (noise - crackle);

            // 갈라짐: 아주 짧고 세게
            float crackEnv = Mathf.Exp(-t * 60f);

            // 우르릉: 천천히 잦아들되 크기가 일렁인다
            float rumbleEnv = Mathf.Exp(-t * 3.2f) * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 7f * t));

            samples[i] = crackle * crackEnv * 0.9f + low * rumbleEnv * 6.0f;
        }

        return Finish("Thunder", samples);
    }

    /// <summary>
    /// 크기를 고르고 앞뒤를 살짝 다듬어 클립으로 만든다.
    /// 끝을 안 다듬으면 파형이 툭 끊겨 "틱" 하는 잡음이 같이 난다.
    /// </summary>
    private static AudioClip Finish(string name, float[] samples)
    {
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
        float gain = peak > 0.0001f ? 0.95f / peak : 1f;

        int fade = Mathf.Min(220, samples.Length / 8);
        for (int i = 0; i < samples.Length; i++)
        {
            float edge = 1f;
            if (i < fade) edge = (float)i / fade;
            else if (i > samples.Length - fade) edge = (float)(samples.Length - i) / fade;

            samples[i] = Mathf.Clamp(samples[i] * gain * edge, -1f, 1f);
        }

        var clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
