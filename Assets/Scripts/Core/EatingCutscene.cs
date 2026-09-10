using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 라멘을 낸 뒤 손님이 먹는 네 컷 연출.
///
///   1컷  일반 화면. 손님이 그릇을 받는다.
///   2컷  위아래 검은 바가 들어오고 얼굴이 화면을 채운다. "…"
///   3컷  번쩍이고 흔들린다. "!!"
///   4컷  바가 걷히고 반짝임이 돈다. 정확도에 따른 반응.
///
/// 컷은 툭 바뀐다. 1배에서 3배로 부드럽게 확대하면 중간에 1.4배·2.2배를 지나면서
/// 픽셀이 반칸에 걸려 뭉개진다. 확대는 정수배로만 하고 전환은 한 프레임에 끝낸다.
/// 원본이 만화 네 컷이라 이쪽이 그림에도 맞다.
///
/// 표정 그림이 아직 없다. 지금은 같은 얼굴이 커졌다 작아지고, 말풍선과 효과로만 박자를 낸다.
/// 표정이 들어오면 각 컷에서 얼굴만 갈아 끼우면 된다.
/// 번개와 우주 배경은 임시로 코드로 찍은 그림이다(Tools 의 makeart 스크립트).
/// 나비·꽃은 아직 없어서 흰 점 반짝임으로 대신하고 있다.
/// </summary>
public class EatingCutscene : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private RectTransform topBar;
    [SerializeField] private RectTransform bottomBar;
    [SerializeField] private Image flash;
    [SerializeField] private RectTransform sparkleRoot;
    [SerializeField] private Image cosmos;
    [SerializeField] private Image bolt;
    [SerializeField] private Image aura;
    [SerializeField] private Sprite[] auraFrames;
    [SerializeField] private Image thumb;
    [SerializeField] private RectTransform customerSlot;
    [SerializeField] private RectTransform customerHead;
    [SerializeField] private OrderScreenUI orderScreen;
    [SerializeField] private CutsceneSfx sfx;

    [Header("박자 길이(초)")]
    [SerializeField] private float slurpSeconds = 1.6f;      // 1 후룩후룩
    [SerializeField] private float zoomStepSeconds = 0.22f;  // 2 클로즈업 한 단
    [SerializeField] private float boltSeconds = 1.6f;       // 3 번개
    [SerializeField] private float cosmosSeconds = 3.5f;     // 4 우주 - 여기서 한 박자 쉰다
    [SerializeField] private float auraSeconds = 1.1f;       // 5 오우라

    /// <summary>오우라가 다 그려진 뒤 천천히 스러지는 시간. 툭 꺼지면 여운이 없다.</summary>
    [SerializeField] private float auraFadeSeconds = 1.2f;

    /// <summary>오우라가 다 스러진 뒤 우주를 조금 더 두는 시간. 곧바로 걷히면 숨 돌릴 틈이 없다.</summary>
    [SerializeField] private float cosmosHoldSeconds = 0.8f;
    [SerializeField] private float thumbSeconds = 3f;        // 7 따봉

    [Header("모양")]
    /// <summary>클로즈업 배율. 정수만 쓴다 — 소수 배율은 픽셀을 반칸에 걸치게 한다.</summary>
    [SerializeField] private int closeUpScale = 3;

    /// <summary>다 들어왔을 때 검은 바 하나의 높이(칸).</summary>
    [SerializeField] private float barHeight = 56f;

    /// <summary>바가 들어오고 걷히는 데 걸리는 시간(초).</summary>
    [SerializeField] private float barSeconds = 0.3f;

    /// <summary>번개가 칠 때 흔들리는 폭(칸). 정수만 쓴다.</summary>
    [SerializeField] private int shakePixels = 3;

    /// <summary>따봉과 함께 도는 반짝임 개수.</summary>
    [SerializeField] private int sparkleCount = 14;

    /// <summary>연출 전 손님 자리. 끝나면 여기로 돌려놓는다.</summary>
    private Vector2 homePosition;
    private Vector3 homeScale;

    private Image[] sparkles;

    private void Awake()
    {
        if (customerSlot != null)
        {
            homePosition = customerSlot.anchoredPosition;
            homeScale = customerSlot.localScale;
        }

        ResetStage();
    }

    /// <summary>
    /// 차례로 보여 준다. 부르는 쪽(GameManager)이 코루틴을 쥔다 —
    /// 화면이 꺼져도 연출이 중간에 끊기지 않게 하기 위해서다.
    ///
    /// 아무 데나 누르면 건너뛴다. 손님마다 15초씩 같은 연출을 다시 보는 것은 지루하다.
    /// 건너뛰면 여기서 바로 끝나고, 부르는 쪽이 이어서 결과창을 띄운다.
    ///
    /// 연출 본체를 따로 돌리고 여기서는 누름만 지켜본다. 단계마다 "건너뛰었나"를 묻는 것보다
    /// 코루틴을 통째로 멈추는 편이 확실하다. 중간에 무엇을 켜 두었든 ResetStage 가 정리한다.
    /// </summary>
    public IEnumerator Play(float accuracy)
    {
        // 이 오브젝트가 꺼져 있으면 여기서 코루틴을 시작할 수 없다. 그때는 예전처럼
        // 부르는 쪽 코루틴 위에서 그대로 돌린다. 대신 건너뛰기는 안 된다.
        if (!gameObject.activeInHierarchy)
        {
            yield return Sequence(accuracy);
            ResetStage();
            yield break;
        }

        playing = true;
        Coroutine body = StartCoroutine(Sequence(accuracy));

        while (playing)
        {
            if (WantsSkip())
            {
                StopCoroutine(body);
                if (sparkling != null) StopCoroutine(sparkling);
                break;
            }

            yield return null;
        }

        playing = false;
        sparkling = null;
        ResetStage();
    }

    /// <summary>누르는 순간 건너뛴다. 뗄 때가 아니라 누를 때라야 반응이 빠르다.</summary>
    private static bool WantsSkip()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;

        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }

    /// <summary>연출 본체. 다 돌면 playing 을 내려 Play 가 빠져나가게 한다.</summary>
    private IEnumerator Sequence(float accuracy)
    {
        ResetStage();

        // 1 — 후룩후룩. 아직 일반 화면이다.
        if (orderScreen != null)
        {
            orderScreen.ShowBubble(true);
            orderScreen.SetBubbleLine(". . .");
        }
        if (sfx != null) sfx.Slurp(slurpSeconds);
        yield return new WaitForSecondsRealtime(slurpSeconds);

        // 2 — 천천히 클로즈업. 정수배로 단을 밟아 올라간다.
        //
        // 말풍선은 여기서 치운다. 얼굴이 화면을 채우는 동안 상자가 남아 있으면
        // 확대된 얼굴 위에 원래 크기의 상자가 덩그러니 떠 있는 꼴이 된다.
        // 다시 꺼내는 것은 7 컷, 원래 크기로 돌아온 뒤다.
        if (orderScreen != null) orderScreen.ShowBubble(false);

        for (int step = 2; step <= Mathf.Max(2, closeUpScale); step++)
        {
            Zoom(step);
            yield return new WaitForSecondsRealtime(zoomStepSeconds);
        }

        // 3 — 시네마틱 바가 들어오고 번개가 한 방.
        yield return MoveBars(0f, barHeight, barSeconds);
        if (orderScreen != null) orderScreen.SetBubbleLine("!!");
        if (sfx != null) sfx.Thunder();
        yield return Strike(boltSeconds);

        // 4 — 배경이 우주로 툭 바뀐다.
        SetCosmos(true);
        yield return new WaitForSecondsRealtime(cosmosSeconds);

        // 5 — 감동 오우라. 다 그려지면 천천히 스러진다.
        yield return PlayAura(auraSeconds);

        // 오우라가 사라진 자리를 잠깐 그대로 둔다. 여기가 이 연출의 여운이다.
        yield return new WaitForSecondsRealtime(cosmosHoldSeconds);

        // 6 — 원래대로.
        SetCosmos(false);
        Zoom(1);
        yield return MoveBars(barHeight, 0f, barSeconds);

        // 7 — 따봉과 대사 한 줄. 화면이 제자리로 돌아왔으니 말풍선도 다시 꺼낸다.
        if (orderScreen != null)
        {
            orderScreen.ShowBubble(true);
            orderScreen.SetBubbleLine(ReactionLine(accuracy));
        }
        if (thumb != null) thumb.enabled = true;
        if (gameObject.activeInHierarchy) sparkling = StartCoroutine(Sparkle(thumbSeconds));
        yield return new WaitForSecondsRealtime(thumbSeconds);

        sparkling = null;
        playing = false;
    }

    /// <summary>연출용 판을 전부 끄고 손님을 제자리로 돌린다.</summary>
    private void ResetStage()
    {
        SetBars(0f);
        SetFlash(0f);
        SetCosmos(false);

        if (bolt != null) bolt.enabled = false;
        if (aura != null) aura.enabled = false;
        if (thumb != null) thumb.enabled = false;

        Zoom(1);
    }

    /// <summary>
    /// 얼굴이 화면 한가운데에 오도록 손님 자리를 정수배로 키운다.
    ///
    /// 자리를 그냥 키우면 자리 한가운데를 기준으로 커져서 얼굴이 화면 위로 빠져나간다.
    /// 얼굴이 자리 안 어디에 있는지 재서, 커진 만큼 자리를 반대로 밀어 준다.
    /// </summary>
    private void Zoom(int scale)
    {
        // 말풍선도 같은 배율로 키운다. 손님만 커지면 말풍선이 혼자 작게 남아 따로 논다.
        // 피벗이 왼쪽 위라 오른쪽·아래로만 자라고, 글에 맞춰 줄어 있어서 3배로도 화면을 안 넘는다.
        ZoomBubble(scale);

        if (customerSlot == null) return;

        if (scale <= 1)
        {
            customerSlot.localScale = homeScale;
            customerSlot.anchoredPosition = homePosition;
            return;
        }

        Vector2 head = HeadCenterInSlot();
        customerSlot.localScale = new Vector3(scale, scale, 1f);
        customerSlot.anchoredPosition = new Vector2(Mathf.Round(-head.x * scale),
                                                    Mathf.Round(-head.y * scale));
    }

    private void ZoomBubble(int scale)
    {
        if (orderScreen == null) return;

        RectTransform bubble = orderScreen.Bubble;
        if (bubble == null) return;

        float k = Mathf.Max(1, scale);
        bubble.localScale = new Vector3(k, k, 1f);
    }

    /// <summary>자리 한가운데를 (0,0)으로 봤을 때 얼굴 한가운데가 어디인가.</summary>
    private Vector2 HeadCenterInSlot()
    {
        if (customerHead == null || customerSlot == null) return Vector2.zero;

        // 얼굴은 자리 아래변을 기준으로 얹혀 있다(CustomerAppearance.SetHead, 피벗 0.5/0).
        float bottom = -customerSlot.rect.height * 0.5f;
        return new Vector2(customerHead.anchoredPosition.x,
                           bottom + customerHead.anchoredPosition.y + customerHead.rect.height * 0.5f);
    }

    private IEnumerator MoveBars(float from, float to, float seconds)
    {
        if (seconds <= 0f)
        {
            SetBars(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            SetBars(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds)));
            yield return null;
        }

        SetBars(to);
    }

    /// <summary>검은 바 높이를 정수 칸으로 맞춘다. 반칸이면 경계에 회색 줄이 낀다.</summary>
    private void SetBars(float height)
    {
        float h = Mathf.Max(0f, Mathf.Round(height));

        if (topBar != null) topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, h);
        if (bottomBar != null) bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, h);
    }

    /// <summary>
    /// 번개 한 방. 심장이 한 번 뛰듯 세게 번쩍이고 잦아든다.
    ///
    /// 여러 줄기를 흩뿌리지 않는다. 굵은 것 하나가 화면을 가로지르는 편이 훨씬 세게 보인다.
    /// 방향(11시 → 5시)은 그림에 이미 구워져 있어 돌리지 않는다.
    /// </summary>
    private IEnumerator Strike(float seconds)
    {
        Vector2 basePosition = customerSlot != null ? customerSlot.anchoredPosition : Vector2.zero;
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float left = Mathf.Clamp01(1f - elapsed / seconds);

            // 켜졌다 꺼졌다 해야 번개로 보인다. 계속 켜 두면 그림이 붙어 있는 것으로 보인다.
            bool lit = left > 0.15f && Mathf.Repeat(elapsed * 22f, 1f) < 0.62f;
            if (bolt != null) bolt.enabled = lit;
            SetFlash(lit ? left * left * 0.9f : 0f);

            // 흔들림도 정수 칸으로만. 반칸을 쓰면 화면 전체가 흐려진다.
            if (customerSlot != null)
            {
                int amplitude = Mathf.RoundToInt(shakePixels * left);
                customerSlot.anchoredPosition = basePosition + new Vector2(
                    Random.Range(-amplitude, amplitude + 1),
                    Random.Range(-amplitude, amplitude + 1));
            }

            yield return null;
        }

        if (customerSlot != null) customerSlot.anchoredPosition = basePosition;
        if (bolt != null) bolt.enabled = false;
        SetFlash(0f);
    }

    /// <summary>
    /// 구워 둔 프레임을 차례로 넘긴다.
    /// 고리 하나를 코드로 키우면 배율이 소수가 되어 픽셀이 깨지므로 그림으로 퍼뜨린다.
    /// </summary>
    private IEnumerator PlayAura(float seconds)
    {
        if (aura == null || auraFrames == null || auraFrames.Length == 0)
        {
            yield return new WaitForSecondsRealtime(seconds);
            yield break;
        }

        aura.enabled = true;
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;

            int frame = Mathf.Clamp(Mathf.FloorToInt(elapsed / seconds * auraFrames.Length),
                                    0, auraFrames.Length - 1);
            aura.sprite = auraFrames[frame];

            yield return null;
        }

        // 마지막 그림을 쥔 채 천천히 스러진다. 그냥 끄면 원이 툭 없어져 여운이 안 남는다.
        Color tint = aura.color;
        float fade = 0f;

        while (fade < auraFadeSeconds)
        {
            fade += Time.unscaledDeltaTime;
            tint.a = 1f - Mathf.Clamp01(fade / auraFadeSeconds);
            aura.color = tint;
            yield return null;
        }

        aura.enabled = false;

        // 다음 손님을 위해 진하기를 되돌린다. 안 하면 두 번째부터 투명한 채로 뜬다.
        tint.a = 1f;
        aura.color = tint;
    }

    /// <summary>연출이 도는 중인가. 본체가 다 돌거나 건너뛰면 내려간다.</summary>
    private bool playing;

    /// <summary>따봉 반짝임. 본체와 따로 도는 것이라 건너뛸 때 같이 멈춰야 한다.</summary>
    private Coroutine sparkling;

    private void SetCosmos(bool on)
    {
        if (cosmos != null) cosmos.enabled = on;
    }

    private void SetFlash(float alpha)
    {
        if (flash == null) return;

        float a = Mathf.Clamp01(alpha);
        Color color = flash.color;
        color.a = a;
        flash.color = color;
        flash.enabled = a > 0f;
    }

    /// <summary>
    /// 4컷 반짝임. 나비·꽃 그림이 없어 흰 점으로 대신한다.
    /// Image 에 그림을 안 넣으면 흰 사각형이 그려지므로 그림 없이 만들 수 있다.
    /// </summary>
    private IEnumerator Sparkle(float seconds)
    {
        if (sparkleRoot == null) yield break;

        EnsureSparkles();

        var origin = new Vector2[sparkles.Length];
        var delay = new float[sparkles.Length];

        for (int i = 0; i < sparkles.Length; i++)
        {
            origin[i] = new Vector2(Random.Range(-46, 47), Random.Range(-40, 21));
            delay[i] = Random.Range(0f, seconds * 0.5f);
            sparkles[i].enabled = false;
        }

        float life = Mathf.Max(0.01f, seconds * 0.5f);
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < sparkles.Length; i++)
            {
                float t = (elapsed - delay[i]) / life;
                if (t < 0f || t > 1f)
                {
                    sparkles[i].enabled = false;
                    continue;
                }

                sparkles[i].enabled = true;

                // 위로 떠오르며 스러진다. 자리는 정수 칸으로 맞춘다.
                sparkles[i].rectTransform.anchoredPosition =
                    new Vector2(origin[i].x, Mathf.Round(origin[i].y + t * 18f));

                sparkles[i].color = new Color(1f, 1f, 1f, 1f - t);
            }

            yield return null;
        }

        for (int i = 0; i < sparkles.Length; i++) sparkles[i].enabled = false;
    }

    /// <summary>반짝임 조각을 처음 쓸 때 한 번 만들어 두고 돌려 쓴다.</summary>
    private void EnsureSparkles()
    {
        if (sparkles != null) return;

        sparkles = new Image[Mathf.Max(0, sparkleCount)];
        for (int i = 0; i < sparkles.Length; i++)
        {
            var go = new GameObject("Sparkle", typeof(RectTransform), typeof(Image));

            var rect = (RectTransform)go.transform;
            rect.SetParent(sparkleRoot, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(2f, 2f);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            sparkles[i] = image;
        }
    }

    /// <summary>
    /// 손님의 한마디. 말투와 표정에 맞는 것을 ReactionLines 가 골라 준다.
    /// 여기서 뽑은 말을 결과창도 그대로 쓴다 — 같은 손님이 두 번 다르게 말하지 않도록.
    /// </summary>
    private static string ReactionLine(float accuracy)
    {
        return ReactionLines.For(accuracy);
    }
}
