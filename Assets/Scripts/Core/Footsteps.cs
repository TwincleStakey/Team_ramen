using System.Collections;
using UnityEngine;

/// <summary>
/// 손님이 걸어오는 발소리.
///
/// 프로젝트에 오디오 파일이 하나도 없다. 대사 톤(DialogueBlip)과 같은 방식으로
/// 파형을 코드로 만들어 쓴다. 낮은 사인파를 아주 빠르게 꺼뜨려 "툭" 하는 소리를 낸다.
///
/// 발소리는 높낮이보다 빨리 꺼지는 것이 중요하다. 여운을 남기면 발소리가 아니라 북소리가 된다.
/// </summary>
public class Footsteps : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.35f;

    /// <summary>걸음과 걸음 사이(초). 사람이 천천히 다가오는 정도로 잡았다.</summary>
    [SerializeField] private float stride = 0.42f;

    /// <summary>걸어 들어오는 그림이 같은 박자를 쓰도록 내어 준다(GameManager.EnterCustomer).</summary>
    public float Stride { get { return stride; } }

    private AudioClip[] steps;
    private Coroutine walking;

    private void Awake()
    {
        // 음원 파일이 있으면 두세 종을 번갈아 쓰고, 하나도 없으면 예전처럼 파형을 만든다.
        var files = new[] { Sfx.Clip("sfx_step_wood_a"), Sfx.Clip("sfx_step_wood_b"), Sfx.Clip("sfx_step_wood_c") };
        steps = System.Array.FindAll(files, c => c != null);
        if (steps.Length == 0) steps = new[] { BuildStep() };
    }

    /// <summary>seconds 동안 뚜벅뚜벅 걷는 소리를 낸다.</summary>
    /// <param name="firstDelay">
    /// 첫 걸음까지 기다리는 시간(초). 음수면 반 박자를 쓴다.
    ///
    /// 걷는 그림과 함께 쓸 때는 0 을 준다. 그림의 첫 발이 화면에 닿는 순간과 소리를 맞추려는 것이다.
    /// 소리만 낼 때는 반 박자 늦춘다 — 앞 손님이 사라지자마자 소리가 나면 그 손님이 낸 것처럼 들린다.
    /// </param>
    public void Walk(float seconds, float firstDelay = -1f)
    {
        if (walking != null) StopCoroutine(walking);
        walking = gameObject.activeInHierarchy
            ? StartCoroutine(WalkRoutine(seconds, firstDelay < 0f ? stride * 0.5f : firstDelay))
            : null;
    }

    private IEnumerator WalkRoutine(float seconds, float firstDelay)
    {
        float elapsed = firstDelay;
        if (firstDelay > 0f) yield return new WaitForSecondsRealtime(firstDelay);

        while (elapsed < seconds)
        {
            // 왼발 오른발이 똑같으면 기계처럼 들린다. 한 걸음씩 조금씩 엇갈리게 둔다.
            Sfx.PlayClip(steps[Random.Range(0, steps.Length)], volume, 1f + Random.Range(-0.08f, 0.08f));

            yield return new WaitForSecondsRealtime(stride);
            elapsed += stride;
        }

        walking = null;
    }

    /// <summary>발 한 번 딛는 소리를 만든다.</summary>
    private static AudioClip BuildStep()
    {
        const int rate = 44100;
        const float seconds = 0.09f;
        const float frequency = 92f;   // 낮게. 이보다 높으면 나무 두드리는 소리가 된다

        int count = Mathf.RoundToInt(rate * seconds);
        var samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float progress = (float)i / count;

            // 몸무게가 바닥을 누르는 낮은 울림.
            float body = Mathf.Sin(2f * Mathf.PI * frequency * t);

            // 신발이 바닥에 쓸리는 소리. 맨 앞에만 잠깐 섞는다.
            // 끝까지 섞으면 발소리가 아니라 잡음이 된다.
            float scuff = (Random.value * 2f - 1f) * Mathf.Clamp01(1f - progress * 6f) * 0.35f;

            // 세제곱으로 빠르게 꺼뜨린다.
            float envelope = Mathf.Pow(1f - progress, 3f);

            samples[i] = (body + scuff) * envelope;
        }

        var clip = AudioClip.Create("Footstep", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
