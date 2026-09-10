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
[RequireComponent(typeof(AudioSource))]
public class Footsteps : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.35f;

    /// <summary>걸음과 걸음 사이(초). 사람이 천천히 다가오는 정도로 잡았다.</summary>
    [SerializeField] private float stride = 0.42f;

    private AudioSource source;
    private AudioClip step;
    private Coroutine walking;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;   // UI 소리라 거리와 무관해야 한다

        step = BuildStep();
    }

    /// <summary>seconds 동안 뚜벅뚜벅 걷는 소리를 낸다.</summary>
    public void Walk(float seconds)
    {
        if (walking != null) StopCoroutine(walking);
        walking = gameObject.activeInHierarchy ? StartCoroutine(WalkRoutine(seconds)) : null;
    }

    private IEnumerator WalkRoutine(float seconds)
    {
        // 첫 걸음은 반 박자 늦게 낸다. 앞 손님이 사라지자마자 소리가 나면 그 손님이 낸 것처럼 들린다.
        float elapsed = stride * 0.5f;
        yield return new WaitForSecondsRealtime(stride * 0.5f);

        while (elapsed < seconds)
        {
            // 왼발 오른발이 똑같으면 기계처럼 들린다. 한 걸음씩 조금씩 엇갈리게 둔다.
            source.pitch = 1f + Random.Range(-0.08f, 0.08f);
            source.PlayOneShot(step, volume);

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
