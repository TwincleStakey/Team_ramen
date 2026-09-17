using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 그 날 새로 열린 재료통의 자물쇠가 풀리는 연출. 하루의 첫 조리 화면에서 한 번 돈다.
///
/// <b>시간을 넣으면 그 순간의 모습이 나오게 짜여 있다</b>(<see cref="ApplyAt"/>). 코루틴은
/// 매 프레임 그걸 부르기만 한다. 이렇게 해 두면 에디터에서 t 를 0.05 씩 밀어 가며 찍어
/// 실제 화면 그대로 미리 볼 수 있다 — 연출을 고를 때 게임을 켜지 않아도 된다.
/// </summary>
public class UnlockSequence : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI names;

    /// <summary>고리가 열린 자물쇠 그림.</summary>
    [SerializeField] private Sprite openSprite;

    /// <summary>「팟!」 빛.</summary>
    [SerializeField] private Sprite glowSprite;

    /// <summary>사방으로 튀는 불꽃 한 점. 빛만으로는 퍼지는 안개로 보이고 터지는 맛이 없다.</summary>
    [SerializeField] private Sprite sparkleSprite;

    // ── 연출 값 ─────────────────────────────────────────────────
    //
    // 세 가지(조용히·팟·장중하게)를 만들어 보고 고른 값이다. 나머지 둘은 지웠다.
    //
    // **팝업은 자물쇠가 다 사라진 뒤에 한 박자 쉬고 뜬다.** 깨지는 것과 동시에 띄웠더니
    // 둘이 한 동작으로 뭉쳐 어느 쪽도 안 읽혔다.

    private const float Shake = 0.28f;          // 덜컹거리는 시간
    private const float ShakePixels = 3f;       // 덜컹거리는 폭
    private const int ShakeTimes = 3;           // 덜컹거리는 횟수
    private const float Open = 0.16f;           // 고리가 열리고 튀어오르는 시간
    private const float HopPixels = 7f;         // 튀어오르는 높이
    private const float Glow = 0.42f;           // 빛과 불꽃이 퍼졌다 사라지는 시간
    private const float GlowSize = 132f;        // 빛이 가장 클 때 크기(칸)
    private const float Vanish = 0.30f;         // 자물쇠가 떨어지며 사라지는 시간
    private const float DropPixels = 14f;       // 떨어지는 거리
    private const float Brighten = 0.24f;       // 통이 밝아지는 시간
    private const int Sparkles = 7;             // 튀는 불꽃 개수
    private const float SparkleReach = 54f;     // 불꽃이 날아가는 거리(칸)

    /// <summary>통 하나가 덜컹거려 열리고 사라지기까지.</summary>
    private const float SlotSpan = Shake + Open + Vanish;

    /// <summary>
    /// 통 사이 시차. <b>한 통이 끝나야 다음 통이 시작한다.</b>
    ///
    /// 겹쳐서 터뜨려 봤더니 세 군데가 동시에 번쩍여 어디를 봐야 할지 알 수 없었다.
    /// 하나씩 끊어야 눈이 따라간다. 사이의 0.08 은 앞 통이 다 사라진 것을 보고 넘어가라는 틈이다.
    /// </summary>
    private const float Stagger = SlotSpan + 0.08f;

    /// <summary>마지막 자물쇠가 다 사라진 뒤 팝업이 뜨기까지 쉬는 시간.</summary>
    private const float PopupDelay = 0.5f;

    private const float PopupIn = 0.34f;        // 팝업이 들어오는 시간
    private const float PopupHold = 1.4f;       // 팝업이 머무는 시간
    private const float PopupOvershoot = 0.1f;  // 팝업이 1 을 넘겼다 돌아오는 정도

    // 판이 뜰 때도 한 번 터뜨린다. 자물쇠와 같은 빛·같은 불꽃을 써야 두 장면이 한 연출로 읽힌다.
    private const float PopupGlowSize = 560f;       // 판 뒤에서 퍼지는 빛의 최대 크기(칸)
    private const float PopupGlowSeconds = 0.5f;    // 그 빛이 퍼졌다 사라지기까지
    private const int PopupSparkles = 12;           // 판 둘레에서 튀는 불꽃 개수
    private const float PopupBurstSeconds = 0.6f;   // 불꽃이 날아가 사라지기까지

    /// <summary>불꽃이 솟는 자리. 판(420x64)의 둘레를 따라 돈다.</summary>
    private const float PopupRingX = 210f;
    private const float PopupRingY = 34f;

    /// <summary>불꽃이 둘레에서 몇 배까지 더 날아가는지.</summary>
    private const float PopupSparkleReach = 1.5f;

    /// <summary>
    /// 연출이 도는 중인가. 도는 동안에는 아무것도 못 만진다.
    ///
    /// 마우스는 팝업 밑에 깔린 투명 판이 막는다(빌더의 Blocker). 그건 클릭만 막으므로
    /// 키로 여는 것(Tab 주문서·B 레시피북)은 <see cref="CookingHotkeys"/> 가 이 값을 보고 막는다.
    /// </summary>
    public static bool IsPlaying { get; private set; }

    /// <summary>이번에 여는 통들. <see cref="Prepare"/> 가 받아 둔다.</summary>
    private SlotLock[] opening;

    /// <summary>통마다 하나씩 만드는 빛. 연출이 끝나면 지운다.</summary>
    private readonly List<Image> glows = new List<Image>();

    /// <summary>통마다 <see cref="Sparkles"/> 개씩 만드는 불꽃. 통 순서대로 이어 담는다.</summary>
    private readonly List<Image> sparkles = new List<Image>();

    /// <summary>자물쇠가 원래 서 있던 자리. 흔들고 떨어뜨린 뒤 되돌릴 때 쓴다.</summary>
    private readonly List<Vector2> homes = new List<Vector2>();

    /// <summary>판 뒤에서 퍼지는 빛.</summary>
    private Image popupGlow;

    /// <summary>판 둘레에서 튀는 불꽃.</summary>
    private readonly List<Image> popupSparkles = new List<Image>();

    /// <summary>연출 전체 길이(초).</summary>
    public float Duration
    {
        get
        {
            return PopupStart + PopupIn + PopupHold;
        }
    }

    /// <summary>
    /// 팝업이 뜨기 시작하는 시각. 마지막 자물쇠가 <b>다 사라진 뒤</b> 한 박자 쉰 자리다.
    /// </summary>
    private float PopupStart
    {
        get
        {
            int count = opening != null ? opening.Length : 1;
            return (count - 1) * Stagger + Shake + Open + Vanish + PopupDelay;
        }
    }

    /// <summary>
    /// 연출에 쓸 것들을 세운다. 빛은 자물쇠와 같은 자리에 같은 부모(재료통) 밑으로 만든다 —
    /// 통이 화면 어디로 옮겨져도 빛이 따라간다.
    /// </summary>
    public void Prepare(SlotLock[] slots)
    {
        Finish();

        opening = slots;
        if (opening == null || opening.Length == 0) return;

        foreach (SlotLock slot in opening)
        {
            Image padlock = slot != null ? slot.Padlock : null;
            if (padlock == null)
            {
                glows.Add(null);
                homes.Add(Vector2.zero);
                continue;
            }

            homes.Add(padlock.rectTransform.anchoredPosition);

            var go = new GameObject("UnlockGlow", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(padlock.rectTransform.parent, false);
            rt.anchorMin = padlock.rectTransform.anchorMin;
            rt.anchorMax = padlock.rectTransform.anchorMax;
            rt.anchoredPosition = padlock.rectTransform.anchoredPosition;

            var image = go.GetComponent<Image>();
            image.sprite = glowSprite;
            image.raycastTarget = false;

            // 자물쇠보다 뒤에 만들어 그 위에 그린다. 빛이 자물쇠에 가리면 「팟」이 안 보인다.
            rt.SetAsLastSibling();

            glows.Add(image);

            for (int s = 0; s < Sparkles; s++)
            {
                sparkles.Add(MakeSparkle(padlock.rectTransform));
            }
        }

        PreparePopupBurst();

        IsPlaying = true;
        if (root != null) root.SetActive(true);
    }

    /// <summary>
    /// 판이 뜰 때 터뜨릴 빛과 불꽃을 세운다.
    ///
    /// 빛은 판보다 <b>앞</b> 순번에 꽂아 판 뒤에서 퍼지게 하고, 불꽃은 뒷순번이라 판 위로 날아간다.
    /// 둘 다 판과 형제다 — 판 밑에 달면 판의 CanvasGroup 알파에 같이 묶여, 판이 흐린 동안
    /// 빛도 흐려서 터지는 맛이 없다.
    /// </summary>
    private void PreparePopupBurst()
    {
        if (panel == null) return;

        RectTransform parent = panel.parent as RectTransform;
        if (parent == null) return;

        popupGlow = MakeEffect("PopupGlow", parent, glowSprite);
        popupGlow.rectTransform.SetSiblingIndex(panel.GetSiblingIndex());

        for (int i = 0; i < PopupSparkles; i++)
        {
            Image spark = MakeEffect("PopupSparkle", parent, sparkleSprite);
            spark.rectTransform.sizeDelta = new Vector2(SparkleSide, SparkleSide);
            popupSparkles.Add(spark);
        }
    }

    /// <summary>연출용 그림 한 장. 꺼진 채로 판과 같은 자리에 선다.</summary>
    private Image MakeEffect(string name, RectTransform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = panel.anchorMin;
        rt.anchorMax = panel.anchorMax;
        rt.anchoredPosition = panel.anchoredPosition;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    /// <summary>불꽃 한 점. 자물쇠와 같은 자리에서 시작해 사방으로 날아간다.</summary>
    private Image MakeSparkle(RectTransform beside)
    {
        var go = new GameObject("UnlockSparkle", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(beside.parent, false);
        rt.anchorMin = beside.anchorMin;
        rt.anchorMax = beside.anchorMax;
        rt.anchoredPosition = beside.anchoredPosition;
        rt.sizeDelta = new Vector2(SparkleSide, SparkleSide);
        rt.SetAsLastSibling();

        var image = go.GetComponent<Image>();
        image.sprite = sparkleSprite;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    /// <summary>불꽃 한 점의 크기. 그림이 13칸이라 그 배수로 둔다.</summary>
    private const float SparkleSide = 13f;

    /// <summary>
    /// 시작하고 <paramref name="t"/> 초가 지난 순간의 모습으로 만든다.
    /// 코루틴도 미리보기도 여기만 부른다.
    /// </summary>
    public void ApplyAt(float t)
    {
        if (opening == null || opening.Length == 0) return;

        for (int i = 0; i < opening.Length; i++)
        {
            SlotLock slot = opening[i];
            if (slot == null) continue;

            ApplySlot(slot, i, t - i * Stagger);
        }

        ApplyPopup(t);
    }

    private void ApplySlot(SlotLock slot, int index, float t)
    {
        Image padlock = slot.Padlock;
        Image bin = slot.Bin;
        Image glow = index < glows.Count ? glows[index] : null;
        Vector2 home = index < homes.Count ? homes[index] : Vector2.zero;

        // 아직 제 차례가 아니다. 잠긴 모습 그대로.
        if (t <= 0f)
        {
            if (padlock != null)
            {
                padlock.enabled = true;
                padlock.rectTransform.anchoredPosition = home;
                SetAlpha(padlock, 1f);
            }
            if (glow != null) glow.enabled = false;
            return;
        }

        // ── 덜컹 ──
        if (t < Shake)
        {
            float phase = t / Shake * ShakeTimes * Mathf.PI * 2f;
            float x = Mathf.Sin(phase) * ShakePixels;
            if (padlock != null)
            {
                padlock.rectTransform.anchoredPosition = home + new Vector2(Mathf.Round(x), 0f);
            }
            if (glow != null) glow.enabled = false;
            return;
        }

        float since = t - Shake;

        // ── 열림 + 튀어오름 ──
        if (padlock != null)
        {
            if (openSprite != null) padlock.sprite = openSprite;

            float hop = since < Open
                ? Mathf.Sin(since / Open * Mathf.PI) * HopPixels   // 올라갔다 내려온다
                : 0f;

            float fall = 0f;
            float fade = 1f;
            if (since > Open)
            {
                float k = Mathf.Clamp01((since - Open) / Vanish);
                fall = -k * k * DropPixels;      // 아래로 갈수록 빨라진다
                fade = 1f - k;
            }

            padlock.rectTransform.anchoredPosition = home + new Vector2(0f, Mathf.Round(hop + fall));
            SetAlpha(padlock, fade);
            padlock.enabled = fade > 0.01f;
        }

        // ── 팟! ──
        if (glow != null)
        {
            float k = Mathf.Clamp01(since / Glow);
            bool on = since < Glow;
            glow.enabled = on;
            if (on)
            {
                float size = Mathf.Lerp(GlowSize * 0.2f, GlowSize, EaseOut(k));
                glow.rectTransform.sizeDelta = new Vector2(size, size);
                SetAlpha(glow, 1f - k * k);
            }
        }

        // ── 사방으로 튀는 불꽃 ──
        if (Sparkles > 0)
        {
            float k = Mathf.Clamp01(since / Glow);
            bool on = since < Glow;

            for (int s = 0; s < Sparkles; s++)
            {
                int at = index * Sparkles + s;
                if (at >= sparkles.Count) break;

                Image spark = sparkles[at];
                if (spark == null) continue;

                spark.enabled = on;
                if (!on) continue;

                // 통마다 각도를 조금 틀어 세 통이 같은 모양으로 터지지 않게 한다.
                float angle = (s / (float)Sparkles) * Mathf.PI * 2f + index * 0.7f;
                float reach = Mathf.Lerp(6f, SparkleReach, EaseOut(k));

                spark.rectTransform.anchoredPosition = home + new Vector2(
                    Mathf.Round(Mathf.Cos(angle) * reach),
                    Mathf.Round(Mathf.Sin(angle) * reach));
                SetAlpha(spark, 1f - k);
            }
        }

        // ── 통이 밝아진다 ──
        if (bin != null)
        {
            float k = Brighten <= 0f ? 1f : Mathf.Clamp01(since / Brighten);
            bin.color = Color.Lerp(SlotLock.LockedTint, Color.white, k);
        }
    }

    private void ApplyPopup(float t)
    {
        if (panel == null) return;

        float start = PopupStart;

        if (t < start)
        {
            SetGroupAlpha(0f);
            ApplyPopupBurst(-1f);
            return;
        }

        ApplyPopupBurst(t - start);

        float k = PopupIn <= 0f ? 1f : Mathf.Clamp01((t - start) / PopupIn);

        // 튀어 들어온다. 1 을 살짝 넘겼다 돌아오면 툭 떨어지는 느낌이 산다.
        float eased = EaseOut(k);
        float scale = Mathf.Lerp(0.6f, 1f + PopupOvershoot, eased) - PopupOvershoot * eased * eased;

        panel.localScale = new Vector3(scale, scale, 1f);
        SetGroupAlpha(eased);
    }

    /// <summary>
    /// 판이 뜨고 <paramref name="since"/> 초 지난 순간의 빛과 불꽃. 음수면 아직 안 뜬 것이라 다 끈다.
    /// </summary>
    private void ApplyPopupBurst(float since)
    {
        if (popupGlow != null)
        {
            bool on = since >= 0f && since < PopupGlowSeconds;
            popupGlow.enabled = on;
            if (on)
            {
                float k = since / PopupGlowSeconds;
                float size = Mathf.Lerp(PopupGlowSize * 0.25f, PopupGlowSize, EaseOut(k));
                popupGlow.rectTransform.sizeDelta = new Vector2(size, size);
                SetAlpha(popupGlow, 1f - k * k);
            }
        }

        bool burst = since >= 0f && since < PopupBurstSeconds;
        for (int i = 0; i < popupSparkles.Count; i++)
        {
            Image spark = popupSparkles[i];
            if (spark == null) continue;

            spark.enabled = burst;
            if (!burst) continue;

            float k = since / PopupBurstSeconds;

            // 한 점에서 뿜지 않고 판 둘레를 따라 돌려 세운다. 가운데서만 나오면
            // 420칸짜리 판 양끝이 허전하다.
            float angle = (i / (float)PopupSparkles) * Mathf.PI * 2f + 0.35f;
            float spread = Mathf.Lerp(1f, PopupSparkleReach, EaseOut(k));

            spark.rectTransform.anchoredPosition = panel.anchoredPosition + new Vector2(
                Mathf.Round(Mathf.Cos(angle) * PopupRingX * spread),
                Mathf.Round(Mathf.Sin(angle) * PopupRingY * spread));
            SetAlpha(spark, 1f - k);
        }
    }

    /// <summary>연출에 쓴 것을 치우고 통을 열린 모습으로 굳힌다.</summary>
    public void Finish()
    {
        for (int i = 0; i < glows.Count; i++)
        {
            if (glows[i] == null) continue;

            // 에디터에서 연출을 한 프레임씩 찍어 보려면 여기가 편집 모드에서도 돌아야 한다.
            // Destroy 는 편집 모드에서 예외를 던져 빛이 씬에 남는다.
            if (Application.isPlaying) Destroy(glows[i].gameObject);
            else DestroyImmediate(glows[i].gameObject);
        }
        glows.Clear();

        for (int i = 0; i < sparkles.Count; i++)
        {
            if (sparkles[i] == null) continue;

            if (Application.isPlaying) Destroy(sparkles[i].gameObject);
            else DestroyImmediate(sparkles[i].gameObject);
        }
        sparkles.Clear();

        for (int i = 0; i < popupSparkles.Count; i++)
        {
            if (popupSparkles[i] == null) continue;

            if (Application.isPlaying) Destroy(popupSparkles[i].gameObject);
            else DestroyImmediate(popupSparkles[i].gameObject);
        }
        popupSparkles.Clear();

        if (popupGlow != null)
        {
            if (Application.isPlaying) Destroy(popupGlow.gameObject);
            else DestroyImmediate(popupGlow.gameObject);
            popupGlow = null;
        }

        if (opening != null)
        {
            for (int i = 0; i < opening.Length; i++)
            {
                SlotLock slot = opening[i];
                if (slot == null) continue;

                // 자물쇠는 다시 잠글 일이 없지만, 그림과 자리는 원래대로 돌려 둔다.
                // 이 통이 다음 판(다시하기)에서 또 잠길 수 있다.
                if (slot.Padlock != null)
                {
                    slot.Padlock.rectTransform.anchoredPosition = i < homes.Count ? homes[i] : Vector2.zero;
                    SetAlpha(slot.Padlock, 1f);
                }

                slot.Refresh();
            }
        }

        homes.Clear();
        opening = null;

        IsPlaying = false;
        if (root != null) root.SetActive(false);
    }

    /// <summary>한 번 돌린다. 하루의 첫 조리 화면에서 부른다.</summary>
    public IEnumerator Play(SlotLock[] slots)
    {
        if (slots == null || slots.Length == 0) yield break;

        Prepare(slots);
        WriteNames(slots);

        float total = Duration;
        float t = 0f;
        float before = -1f;

        while (t < total)
        {
            ApplyAt(t);
            PlaySounds(before, t);
            before = t;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyAt(total);
        PlaySounds(before, total);
        Finish();
    }

    /// <summary>
    /// <paramref name="before"/> 와 <paramref name="now"/> 사이를 지나친 소리를 낸다.
    ///
    /// <see cref="ApplyAt"/> 에 넣지 않는다. 그쪽은 같은 시각으로 여러 번 불려도 같은 그림이
    /// 나와야 하는 함수라(에디터 미리보기가 그렇게 쓴다) 소리를 넣으면 프레임마다 다시 울린다.
    /// </summary>
    private void PlaySounds(float before, float now)
    {
        for (int i = 0; i < opening.Length; i++)
        {
            float start = i * Stagger;

            if (Crossed(before, now, start)) Sfx.Play("sfx_unlock_rattle", 0.5f);
            if (Crossed(before, now, start + Shake)) Sfx.Play("sfx_unlock_pop", 0.6f);
        }

        if (Crossed(before, now, PopupStart)) Sfx.Play("sfx_unlock_fanfare", 0.7f);
    }

    private static bool Crossed(float before, float now, float at)
    {
        return before < at && now >= at;
    }

    /// <summary>팝업에 오늘 열린 재료 이름을 적는다.</summary>
    public void WriteNames(SlotLock[] slots)
    {
        if (title != null) title.text = "새 재료가 들어왔습니다";
        if (names == null || slots == null) return;

        var parts = new List<string>();
        foreach (SlotLock slot in slots)
        {
            if (slot != null) parts.Add(OrderManager.GetKoreanIngredientName(slot.Type));
        }

        names.text = string.Join(" · ", parts);
    }

    private void SetGroupAlpha(float a)
    {
        if (panel == null) return;

        var group = panel.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = a;
    }

    private static void SetAlpha(Image image, float a)
    {
        if (image == null) return;

        Color c = image.color;
        image.color = new Color(c.r, c.g, c.b, a);
    }

    /// <summary>끝에서 부드럽게 멈춘다.</summary>
    private static float EaseOut(float k)
    {
        return 1f - (1f - k) * (1f - k);
    }
}
