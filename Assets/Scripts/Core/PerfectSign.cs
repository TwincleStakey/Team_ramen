using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정확도 100% 일 때 "완벽한 한 그릇!" 팻말이 팍 떴다가 위로 스윽 사라진다.
/// 기획서 v1.2 7.3 — "100% 주문에는 '완벽한 한 그릇!' 연출을 제공한다".
///
/// 시간은 실시간으로 잰다. 확인창이 떠서 게임이 멈춰 있어도 연출은 흘러야 한다.
/// </summary>
public class PerfectSign : MonoBehaviour
{
    /// <summary>팻말 전체. 평소에는 꺼 둔다. 빌더가 꽂아 준다.</summary>
    [SerializeField] private RectTransform sign;

    /// <summary>같이 흐려질 것들을 한 번에 다루려고 CanvasGroup 을 쓴다.</summary>
    [SerializeField] private CanvasGroup group;

    [Header("팍 뜨는 구간")]
    [SerializeField] private float popSeconds = 0.22f;

    /// <summary>어느 크기에서 시작하는가.</summary>
    [SerializeField] private float popFrom = 0.55f;

    /// <summary>
    /// 제자리(1.0)를 얼마나 넘겼다 돌아오는가.
    ///
    /// 딱 1.0 에서 멈추면 커지다 만 것처럼 보인다. 살짝 넘쳤다 돌아와야 튀어나온 맛이 난다.
    /// </summary>
    [SerializeField] private float popOvershoot = 1.16f;

    /// <summary>뜨는 순간 화면이 한 번 번쩍이는 시간(초)과 세기.</summary>
    [SerializeField] private float flashSeconds = 0.12f;
    [SerializeField] private float flashStrength = 0.5f;

    /// <summary>뜬 뒤 좌우로 떨리는 시간·폭·빠르기. 나무 팻말이 꽂힌 느낌을 낸다.</summary>
    [SerializeField] private float shakeSeconds = 0.38f;
    [SerializeField] private float shakeAmount = 5f;
    [SerializeField] private float shakeHz = 6f;

    /// <summary>반짝임이 바깥으로 흩어지는 구간. 팻말이 다 뜨기 전에 시작해 겹친다.</summary>
    [SerializeField] private float burstDelay = 0.12f;
    [SerializeField] private float burstSeconds = 0.55f;
    [SerializeField] private float burstFrom = 40f;
    [SerializeField] private float burstTo = 165f;

    /// <summary>바깥으로 퍼질 때 세로를 얼마나 눌러 그리는가. 1 이면 정원, 낮으면 납작하다.</summary>
    [SerializeField] private float burstFlatten = 0.55f;

    [Header("머무는 구간")]
    [SerializeField] private float holdSeconds = 0.85f;

    [Header("스윽 사라지는 구간")]
    [SerializeField] private float riseSeconds = 0.6f;

    /// <summary>위로 얼마나 올라가며 사라지는가(칸).</summary>
    [SerializeField] private float riseDistance = 70f;

    /// <summary>화면을 한 번 덮어 번쩍이는 흰 판. 빌더가 꽂아 준다.</summary>
    [SerializeField] private Image flash;

    /// <summary>바깥으로 흩어지는 반짝임들. 빌더가 여덟 방향으로 만들어 꽂아 준다.</summary>
    [SerializeField] private RectTransform[] sparkles;

    private Vector2 home;

    private void Awake()
    {
        if (sign != null) home = sign.anchoredPosition;
        Hide();
    }

    private void Hide()
    {
        if (group != null) group.alpha = 0f;
        if (sign != null) sign.gameObject.SetActive(false);

        SetFlash(0f);
        SetSparkles(2f);
    }

    /// <summary>한 번 보여 주고 끝날 때까지 기다린다. GameManager 가 부른다.</summary>
    public IEnumerator Play()
    {
        if (sign == null || group == null) yield break;

        sign.gameObject.SetActive(true);
        group.alpha = 1f;
        sign.anchoredPosition = home;
        sign.localScale = Vector3.one * popFrom;
        Sfx.Play("sfx_flow_perfect", 0.7f);

        // 팍 · 번쩍 · 흔들림 · 반짝임이 겹쳐 도는 구간.
        // 하나씩 차례로 돌리면 "번쩍 하고 나서 팻말이 뜬다" 처럼 끊겨 보인다.
        float elapsed = 0f;
        float span = popSeconds + holdSeconds;

        while (elapsed < span)
        {
            // 팍. 앞 절반은 popFrom → popOvershoot, 뒤 절반은 popOvershoot → 1.
            if (elapsed < popSeconds)
            {
                float k = elapsed / popSeconds;
                float scale = k < 0.5f
                    ? Mathf.Lerp(popFrom, popOvershoot, k * 2f)
                    : Mathf.Lerp(popOvershoot, 1f, (k - 0.5f) * 2f);
                sign.localScale = Vector3.one * scale;
            }
            else
            {
                sign.localScale = Vector3.one;

                // 흔들림. 팻말이 다 뜬 뒤부터 재고 점점 잦아든다.
                float e = elapsed - popSeconds;
                float damp = Mathf.Max(0f, 1f - e / shakeSeconds);
                float dx = Mathf.Sin(e * shakeHz * 2f * Mathf.PI) * shakeAmount * damp;
                sign.anchoredPosition = home + new Vector2(dx, 0f);
            }

            SetFlash(elapsed < flashSeconds
                ? flashStrength * (1f - elapsed / flashSeconds)
                : 0f);

            SetSparkles((elapsed - burstDelay) / burstSeconds);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        sign.localScale = Vector3.one;
        sign.anchoredPosition = home;
        SetFlash(0f);
        SetSparkles(2f);

        // 스윽. 위로 오르며 흐려진다.
        for (float t = 0f; t < riseSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / riseSeconds;

            // 끝으로 갈수록 빨라진다. 등속이면 사라지다 만 것처럼 늘어진다.
            sign.anchoredPosition = home + new Vector2(0f, riseDistance * k * k);
            group.alpha = 1f - k;
            yield return null;
        }

        Hide();
    }

    private void SetFlash(float strength)
    {
        if (flash == null) return;

        bool on = strength > 0.001f;
        if (flash.enabled != on) flash.enabled = on;
        if (!on) return;

        Color c = flash.color;
        flash.color = new Color(c.r, c.g, c.b, strength);
    }

    /// <summary>
    /// 반짝임을 바깥으로 흩뿌린다. k 가 0~1 밖이면 감춘다.
    ///
    /// 가로로 더 넓게 퍼뜨린다(burstFlatten). 화면이 16:9 라 정원으로 퍼뜨리면
    /// 위아래가 먼저 화면 밖으로 나가 좌우만 남은 것처럼 보인다.
    /// </summary>
    private void SetSparkles(float k)
    {
        if (sparkles == null) return;

        bool on = k >= 0f && k <= 1f;
        float radius = Mathf.Lerp(burstFrom, burstTo, Mathf.Clamp01(k));
        float scale = on ? Mathf.Max(0.15f, 1f - k) : 0f;

        for (int i = 0; i < sparkles.Length; i++)
        {
            if (sparkles[i] == null) continue;

            if (sparkles[i].gameObject.activeSelf != on) sparkles[i].gameObject.SetActive(on);
            if (!on) continue;

            float angle = i * Mathf.PI * 2f / sparkles.Length;
            sparkles[i].anchoredPosition = new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius * burstFlatten);
            sparkles[i].localScale = Vector3.one * scale;
        }
    }
}
