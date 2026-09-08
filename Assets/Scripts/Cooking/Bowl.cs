using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 중앙 그릇. 재료를 받고, 제출할 때 통째로 제출 영역으로 끌린다.
/// 담긴 수량 데이터(bowl)와 그릇 위 표시(Contents 아이콘)는 분리해서 관리한다. (기획서 15장)
/// 국물은 아이콘이 아니라 그릇 그림 자체를 바꿔서 표현한다.
/// </summary>
public class Bowl : MonoBehaviour, IDropHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // 그릇 상태별 그림. RamenLayoutBuilder가 넣어 준다.
    public Sprite emptyBowlSprite;
    public Sprite brothBowlSprite;
    public Sprite shioBowlSprite;
    public Sprite shoyuBowlSprite;
    public Sprite tonkotsuBowlSprite;

    // ── 붓기 애니메이션 ──────────────────────────────────────────
    // 국물이 차오르거나 타래 색이 물드는 장면. RamenLayoutBuilder가 시트를 잘라 넣어 준다.
    public Sprite[] brothPourFrames;
    public Sprite[] shioPourFrames;
    public Sprite[] shoyuPourFrames;
    public Sprite[] tonkotsuPourFrames;

    /// <summary>
    /// 붓기 애니메이션 속도(초당 프레임). 값이 작을수록 느리다. 육수와 타래에 같이 쓴다.
    /// 인스펙터에서 바꾸면 바로 반영되지만 빌더를 다시 돌리면 이 기본값으로 되돌아간다.
    /// 속도를 굳히려면 아래 숫자를 고칠 것.
    /// </summary>
    public float pourFps = 10f;

    /// <summary>육수는 앞에서 몇 장까지 쓸지. 시트에는 더 있지만 뒤쪽은 안 쓴다.</summary>
    public int brothFrameCount = 5;

    /// <summary>타래는 앞에서 몇 장까지 쓸지. 이미 국물이 있으니 짧게 스친다.</summary>
    public int tareFrameCount = 2;


    private Coroutine brothPour;

    /// <summary>드래그 전 그리기 순서. 끝나면 여기로 돌려놓는다.</summary>
    private int siblingIndexBeforeDrag = -1;

    // 지금 그릇에 담긴 재료
    private readonly Dictionary<IngredientType, int> bowl = new Dictionary<IngredientType, int>();

    // 폐기 버튼으로 버린 재료의 누적. 제출할 때 함께 넘긴다.
    private readonly Dictionary<IngredientType, int> discarded = new Dictionary<IngredientType, int>();

    // ── 투입 규칙 ────────────────────────────────────────────────
    // 타래 3종은 합쳐서 1개. 하나라도 들어 있으면 전부 막으므로 "1회 제한"과 "교체 불가"가 동시에 걸린다.
    // 타래는 육수가 먼저 들어가야 넣을 수 있다. 빈 그릇에 타래만 붓는 상태를 만들지 않기 위해서다.
    private const int MaxBroth = 1;
    private const int MaxNoodles = 1;   // B의 RecipeGenerator도 면을 항상 1로 둔다
    // 토핑·조미료는 상한 없음. 초과 투입은 정산에서 감점될 뿐 투입 자체는 막지 않는다.

    // ── 그릇 안 표시 ─────────────────────────────────────────────
    private const string ContentsName = "Contents";

    /// <summary>면 사리는 그릇 그림과 같은 판에 그려져 있어 그릇과 같은 크기로 겹쳐 놓는다.</summary>
    private static readonly Vector2 NoodleNestSize = new Vector2(768f, 768f);

    /// <summary>
    /// 재료 그림 한 변. 64px 원본의 3배다.
    /// 그릇은 6배인데 재료만 3배인 이유는, 재료 그림이 재료통에서도 알아보이도록
    /// 원래 크게 확대해서 그려져 있기 때문이다. 그릇과 같은 6배로 띄우면
    /// 차슈 한 장이 국물 절반을 덮어서 실제 라멘과 전혀 다르게 보인다.
    /// </summary>
    private const float ToppingSide = 192f;

    // 한 재료의 최대 수량 = 기본 레시피 + 추가 3. B의 GetBaseRecipe 최대가
    // 차슈·멘마 2라 최대 5개까지 나온다. 아래 자리표를 그만큼 채워 두고,
    // 넘치면 처음 자리로 돌아가 겹친다.

    /// <summary>몇 번째로 담기느냐에 따라 달라지는 자리와 기울기.</summary>
    private class Placement
    {
        public readonly Vector2 Pos;
        public readonly float Angle;

        public Placement(float x, float y, float angle)
        {
            Pos = new Vector2(x, y);
            Angle = angle;
        }
    }

    /// <summary>재료 하나의 배치 규칙.</summary>
    private class ToppingLayout
    {
        /// <summary>45도 시점이라 뒷줄은 앞줄보다 먼저 그려야 가려지는 순서가 맞는다.</summary>
        public readonly bool Back;
        public readonly Placement[] Spots;

        public ToppingLayout(bool back, params Placement[] spots)
        {
            Back = back;
            Spots = spots;
        }
    }

    // 그릇 원본(128px)에서 국물은 x 14~114, y 41~83이다. 6배로 띄우면
    // 그릇 중심 기준으로 가로 ±300, 세로 -114~138이 국물 면이 된다.
    // 아래 자리는 실제 라멘 사진의 배치를 따라 잡은 것이다.
    // 차슈·멘마는 뒤쪽 좌우로 눕히고, 계란은 앞쪽에 두고, 파는 마지막에 가운데 위로 얹는다.
    private static readonly Dictionary<IngredientType, ToppingLayout> Layouts =
        new Dictionary<IngredientType, ToppingLayout>
        {
            // 차슈: 왼쪽 뒤에 부채처럼 겹쳐 눕힌다
            { IngredientType.Chashu, new ToppingLayout(true,
                new Placement(-118f,  48f, -10f),
                new Placement(-172f,  12f,   8f),
                new Placement( -78f,   6f, -20f),
                new Placement(-140f,  86f,  16f),
                new Placement( -52f,  62f,  -4f)) },

            // 멘마: 오른쪽에 비스듬히 눕힌다
            { IngredientType.Menma, new ToppingLayout(true,
                new Placement( 150f,  40f, -12f),
                new Placement( 190f,   0f,   8f),
                new Placement( 118f, -22f, -22f),
                new Placement( 205f,  72f,  16f),
                new Placement(  98f,  70f,  -4f)) },

            // 김: 자리 잡기가 까다로워 임시로 뒤쪽에 세워만 둔다. (다음 작업)
            { IngredientType.Nori, new ToppingLayout(true,
                new Placement(  40f,  60f,  -6f),
                new Placement(  72f,  48f,   4f),
                new Placement(  12f,  70f, -12f),
                new Placement(  96f,  36f,  10f)) },

            // 계란: 앞쪽 가운데. 자른 면이 보이게 눕힌다
            { IngredientType.Egg, new ToppingLayout(false,
                new Placement( -30f, -46f,   0f),
                new Placement(  26f, -58f,  12f),
                new Placement( -88f, -60f, -14f),
                new Placement(  74f, -34f,   6f)) },

            // 숙주: 가운데에 소복하게
            { IngredientType.BeanSprout, new ToppingLayout(false,
                new Placement(  16f,  16f,  -6f),
                new Placement( -34f,  36f,   8f),
                new Placement(  66f,  40f, -14f),
                new Placement( -14f, -12f,   4f)) },

            // 목이버섯: 오른쪽 앞
            { IngredientType.WoodEar, new ToppingLayout(false,
                new Placement( 108f, -48f,   0f),
                new Placement( 158f, -22f,  10f),
                new Placement(  72f, -18f,  -8f),
                new Placement( 132f,   8f,   5f)) },

            // 파: 마지막에 가운데 위로 뿌린다. 기울이면 오히려 어색해서 각도는 안 준다
            { IngredientType.GreenOnion, new ToppingLayout(false,
                new Placement(   0f,  74f,   0f),
                new Placement( -52f,  92f,   0f),
                new Placement(  50f,  88f,   0f),
                new Placement(   0f,  44f,   0f)) },
        };

    // ── 거부 표시 ────────────────────────────────────────────────
    private static readonly Color RejectColor = new Color(1f, 0.45f, 0.4f);
    private const float BlinkSeconds = 0.18f;

    private RectTransform rect;
    private Image image;
    private CanvasGroup canvasGroup;
    private Canvas canvas;
    private Transform contents;
    private Vector2 homePosition;
    private Coroutine blink;

    // 면 사리는 토핑보다 뒤에 와야 한다. 뒷줄 토핑을 끼워 넣을 기준점.
    private int backInsertIndex;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        contents = transform.Find(ContentsName);

        homePosition = rect.anchoredPosition;
        RefreshBowlSprite();

        if (contents == null)
        {
            Debug.LogWarning("[Bowl] Contents 자식을 찾지 못했습니다. Tools > Ramen > Build Cooking Layout을 다시 실행해 주세요.");
        }
    }

    // ── 재료 받기 ────────────────────────────────────────────────

    /// <summary>재료통에서 끌어온 고체를 놓았을 때. uGUI가 슬롯의 OnEndDrag보다 먼저 부른다.</summary>
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        var slot = eventData.pointerDrag.GetComponent<IngredientSlot>();
        if (slot != null) TryAdd(slot.type, slot.bowlSprite);
    }

    /// <summary>국자나 병을 든 채로 그릇을 클릭하면 넣는다. 액체·조미료는 끌지 않고 클릭으로 옮긴다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        CookingCursor cursor = CookingCursor.Instance;
        if (cursor == null || !cursor.IsHolding) return;

        if (TryAdd(cursor.Held, null)) cursor.Deliver();
    }

    /// <summary>규칙에 맞으면 담고 그림을 얹는다. 어기면 그릇 전체를 붉게 깜빡인다.</summary>
    public bool TryAdd(IngredientType type, Sprite icon)
    {
        if (!IsAllowed(type))
        {
            Reject(type);
            return false;
        }

        int count = CountIn(bowl, type) + 1;
        bowl[type] = count;
        AddIcon(type, icon, count);

        // 앞의 연출이 남아 있으면 새 상태를 덮어쓴다. 여기서 확실히 끊는다.
        StopBrothPour();

        // 육수와 타래는 그릇 그림을 바로 갈아 끼우지 않고 부어지는 장면을 보여 준다.
        Sprite[] frames = PourFramesFor(type);
        if (frames != null && frames.Length > 0)
        {
            int frameCount = IsTare(type) ? tareFrameCount : brothFrameCount;
            PlayPour(frames, frameCount);
        }
        else
        {
            RefreshBowlSprite();
        }
        return true;
    }

    /// <summary>타래가 하나라도 들어가 있는가.</summary>
    private bool HasTare
    {
        get
        {
            return bowl.ContainsKey(IngredientType.ShioTare)
                || bowl.ContainsKey(IngredientType.ShoyuTare)
                || bowl.ContainsKey(IngredientType.TonkotsuBase);
        }
    }

    private bool IsAllowed(IngredientType type)
    {
        // 베이스는 타래 → 육수 → 면 순서로만 넣을 수 있다.
        if (IsTare(type))
        {
            // 타래가 맨 처음이다. 하나라도 있으면 같은 타래든 다른 타래든 전부 거부한다.
            return !HasTare;
        }

        if (type == IngredientType.Broth)
        {
            if (!HasTare) return false;
            return CountIn(bowl, type) < MaxBroth;
        }

        if (type == IngredientType.Noodles)
        {
            if (!bowl.ContainsKey(IngredientType.Broth)) return false;
            return CountIn(bowl, type) < MaxNoodles;
        }

        // 토핑과 조미료는 순서를 따지지 않는다.
        return true;
    }

    private static bool IsTare(IngredientType type)
    {
        return type == IngredientType.ShioTare
            || type == IngredientType.ShoyuTare
            || type == IngredientType.TonkotsuBase;
    }

    private void Reject(IngredientType type)
    {
        string reason;
        if (type == IngredientType.Broth && !HasTare) reason = "타래를 먼저 넣어야 합니다.";
        else if (type == IngredientType.Noodles && !bowl.ContainsKey(IngredientType.Broth)) reason = "육수를 먼저 부어야 합니다.";
        else reason = "이미 넣었거나 지금 넣을 수 없는 재료입니다.";

        Debug.Log("[투입 거부] " + type + " — " + reason);
        if (blink != null) StopCoroutine(blink);
        blink = StartCoroutine(Blink());
    }

    private IEnumerator Blink()
    {
        Tint(RejectColor);
        yield return new WaitForSecondsRealtime(BlinkSeconds);
        Tint(Color.white);
        blink = null;
    }

    /// <summary>그릇만이 아니라 이미 담긴 재료까지 함께 물들여야 거부가 눈에 들어온다.</summary>
    private void Tint(Color color)
    {
        image.color = color;
        if (contents == null) return;

        for (int i = 0; i < contents.childCount; i++)
        {
            Image child = contents.GetChild(i).GetComponent<Image>();
            if (child != null) child.color = color;
        }
    }

    // ── 그릇 드래그 → 제출 ────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 이걸 끄지 않으면 그릇이 자기 밑의 SubmitZone을 가려서 제출이 영영 안 된다.
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

        // 끄는 동안에만 맨 위로 올린다. 끝나고 되돌리지 않으면 그릇이 커서와 결과창까지
        // 영원히 덮어 버려서, 마우스 위치도 안 보이고 결과창 버튼도 안 눌린다.
        siblingIndexBeforeDrag = transform.GetSiblingIndex();
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        // delta는 실제 화면 픽셀, anchoredPosition은 캔버스 기준 단위라 스케일로 나눈다.
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        rect.anchoredPosition += eventData.delta / scale;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        // SubmitZone.OnDrop은 이 시점보다 먼저 끝나 있다. 제출됐든 아니든 그릇은 원래 자리로 돌아간다.
        rect.anchoredPosition = homePosition;

        if (siblingIndexBeforeDrag >= 0)
        {
            transform.SetSiblingIndex(siblingIndexBeforeDrag);
            siblingIndexBeforeDrag = -1;
        }
    }

    /// <summary>SubmitZone이 부른다. 넘기고 나면 다음 손님을 위해 그릇과 폐기 기록을 모두 비운다.</summary>
    public void Submit()
    {
        if (bowl.Count == 0)
        {
            Debug.Log("[제출] 그릇이 비어 있어 제출하지 않았습니다.");
            return;
        }

        // B의 RamenState는 폐기 딕셔너리를 인자로 받기만 하고 저장하지 않는다.
        // 그래서 폐기분은 여기서 직접 로그로 남긴다. (노트 2장)
        Debug.Log("[폐기 누적] " + Describe(discarded));

        var state = new RamenState(bowl, discarded);

        if (GameManager.Instance != null) GameManager.Instance.SubmitRamen(state);
        else Debug.LogWarning("[Bowl] 씬에 GameManager가 없습니다.");

        ClearBowl();
        discarded.Clear();
    }

    // ── 폐기 ─────────────────────────────────────────────────────

    /// <summary>폐기 버튼. 그릇 내용을 폐기 기록에 누적하고 비운다. 주문은 그대로 유지된다.</summary>
    public void Discard()
    {
        if (bowl.Count == 0)
        {
            Debug.Log("[폐기] 그릇이 비어 있습니다.");
            return;
        }

        foreach (KeyValuePair<IngredientType, int> pair in bowl)
        {
            discarded[pair.Key] = CountIn(discarded, pair.Key) + pair.Value;
        }

        Debug.Log("[폐기] " + Describe(bowl) + " → 누적 " + Describe(discarded));
        ClearBowl();
    }

    // ── 표시와 도우미 ────────────────────────────────────────────

    /// <summary>그 재료를 부을 때 보여 줄 장면. 없으면 null.</summary>
    private Sprite[] PourFramesFor(IngredientType type)
    {
        switch (type)
        {
            case IngredientType.Broth: return brothPourFrames;
            case IngredientType.ShioTare: return shioPourFrames;
            case IngredientType.ShoyuTare: return shoyuPourFrames;
            case IngredientType.TonkotsuBase: return tonkotsuPourFrames;
            default: return null;
        }
    }

    /// <summary>붓는 장면을 한 번 재생하고 평소 그릇 그림으로 안착한다.</summary>
    private void PlayPour(Sprite[] frames, int count)
    {
        StopBrothPour();
        brothPour = StartCoroutine(PourRoutine(frames, count));
    }

    private void StopBrothPour()
    {
        if (brothPour != null)
        {
            StopCoroutine(brothPour);
            brothPour = null;
        }
    }

    private IEnumerator PourRoutine(Sprite[] frames, int count)
    {
        // fps가 0이나 음수면 아예 안 넘어가므로 최소값을 둔다.
        float perFrame = 1f / Mathf.Max(0.1f, pourFps);
        int last = Mathf.Clamp(count, 1, frames.Length);

        for (int i = 0; i < last; i++)
        {
            if (frames[i] != null) image.sprite = frames[i];
            yield return new WaitForSeconds(perFrame);
        }

        RefreshBowlSprite();
        brothPour = null;
    }

    /// <summary>국물 상태에 맞는 그릇 그림을 고른다. 타래가 있으면 타래 색이 이긴다.</summary>
    private void RefreshBowlSprite()
    {

        Sprite next = emptyBowlSprite;

        if (bowl.ContainsKey(IngredientType.ShioTare)) next = shioBowlSprite;
        else if (bowl.ContainsKey(IngredientType.ShoyuTare)) next = shoyuBowlSprite;
        else if (bowl.ContainsKey(IngredientType.TonkotsuBase)) next = tonkotsuBowlSprite;
        else if (bowl.ContainsKey(IngredientType.Broth)) next = brothBowlSprite;

        if (next != null) image.sprite = next;
    }

    private void AddIcon(IngredientType type, Sprite icon, int count)
    {
        if (contents == null) return;

        // 타래와 육수는 그릇 그림 자체가 바뀌므로 따로 얹을 게 없다.
        if (IsTare(type) || type == IngredientType.Broth) return;

        // 조미료는 그릇용 그림이 없다. 수량만 세고 화면에는 안 나온다.
        if (icon == null) return;

        if (type == IngredientType.Noodles)
        {
            // 면 사리는 국물 바로 위, 토핑 아래.
            RectTransform nest = CreateIcon(type, icon, Vector2.zero, NoodleNestSize, 0f);
            nest.SetSiblingIndex(0);
            backInsertIndex = 1;
            return;
        }

        if (!Layouts.TryGetValue(type, out ToppingLayout layout))
        {
            Debug.LogWarning("[Bowl] 그릇 안 자리가 정해지지 않은 재료입니다: " + type);
            return;
        }

        Placement spot = layout.Spots[(count - 1) % layout.Spots.Length];
        RectTransform placed = CreateIcon(type, icon, spot.Pos,
                                          new Vector2(ToppingSide, ToppingSide), spot.Angle);

        // 뒷줄은 앞줄보다 먼저 그려야 45도 시점에서 가려지는 순서가 맞는다.
        if (layout.Back) placed.SetSiblingIndex(backInsertIndex++);
    }

    private RectTransform CreateIcon(IngredientType type, Sprite icon, Vector2 pos, Vector2 size, float angle)
    {
        var go = new GameObject("Icon_" + type, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(contents, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        var img = go.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;

        // 아이콘이 레이캐스트를 먹으면 그릇의 OnDrop이 가려진다.
        img.raycastTarget = false;
        return rt;
    }

    private void ClearBowl()
    {
        StopBrothPour();

        bowl.Clear();
        backInsertIndex = 0;
        RefreshBowlSprite();
        image.color = Color.white;

        if (contents == null) return;

        for (int i = contents.childCount - 1; i >= 0; i--)
        {
            Destroy(contents.GetChild(i).gameObject);
        }
    }

    private static int CountIn(Dictionary<IngredientType, int> dict, IngredientType type)
    {
        return dict.TryGetValue(type, out int value) ? value : 0;
    }

    private static string Describe(Dictionary<IngredientType, int> dict)
    {
        if (dict == null || dict.Count == 0) return "(없음)";

        var sb = new StringBuilder();
        foreach (KeyValuePair<IngredientType, int> pair in dict)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(pair.Key).Append("=").Append(pair.Value);
        }
        return sb.ToString();
    }
}
