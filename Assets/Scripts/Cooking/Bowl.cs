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
    /// <summary>아직 아무것도 안 들어간 그릇. 타래를 붓기 전까지는 이 그림이다.</summary>
    public Sprite emptyBowlSprite;

    // ── 그릇 8프레임 시트 ────────────────────────────────────────
    // 타래 종류마다 한 장씩. 한 시트가 조리 전 과정을 담는다.
    //   0~3  타래를 부어 국물이 생기는 구간
    //   4~6  육수를 부어 국물이 차오르는 구간
    //   7    면을 넣은 모습
    // RamenLayoutBuilder가 시트를 잘라 넣어 준다.
    public Sprite[] shioFrames;
    public Sprite[] shoyuFrames;
    public Sprite[] tonkotsuFrames;

    // ── 그릇 16프레임 찰랑임 시트 ────────────────────────────────
    // 토핑을 올렸을 때 국물이 흔들리는 한 바퀴. 마찬가지로 타래 종류마다 한 장씩이다.
    // 0번과 15번이 같은 그림이라 어디서 끊어도 이어진다.
    //
    // 정지 그림으로는 쓰지 않는다. 한 바퀴 돌고 나면 붓기 시트 7번(면)으로 돌아간다.
    // 이 시트는 국물이 더 높아서 면이 잠겨 보이는데, 멈춰 있을 때는 면이 보여야 한다.
    public Sprite[] shioToppingFrames;
    public Sprite[] shoyuToppingFrames;
    public Sprite[] tonkotsuToppingFrames;

    /// <summary>투입을 거부했을 때 이유를 띄우는 안내. RamenLayoutBuilder가 꽂아 준다.</summary>
    public IngredientToast toast;

    /// <summary>
    /// 붓기 애니메이션 속도(초당 프레임). 값이 작을수록 느리다.
    /// 인스펙터에서 바꾸면 바로 반영되지만 빌더를 다시 돌리면 이 기본값으로 되돌아간다.
    /// 속도를 굳히려면 아래 숫자를 고칠 것.
    /// </summary>
    public float pourFps = 10f;

    /// <summary>
    /// 찰랑임 속도(초당 프레임). 16장짜리라 붓기와 같은 속도로 돌리면 1.6초나 걸려 늘어진다.
    /// </summary>
    public float rippleFps = 20f;

    // 시트 안에서 각 구간이 차지하는 자리.
    private const int TareFirstFrame = 0;
    private const int TareLastFrame = 3;
    private const int BrothFirstFrame = 4;
    private const int BrothLastFrame = 6;
    private const int NoodleFrame = 7;

    // 찰랑임 시트는 처음부터 끝까지 한 구간이다.
    private const int RippleFirstFrame = 0;
    private const int RippleLastFrame = 15;


    private Coroutine brothPour;

    /// <summary>상한을 정하려면 지금 손님이 시킨 메뉴를 알아야 한다. 처음 쓸 때 한 번 찾아 둔다.</summary>
    private OrderManager orderManager;

    /// <summary>제출 영역. 끌 때마다 겹치는지 물어본다. 처음 쓸 때 한 번 찾아 둔다.</summary>
    private SubmitZone submitZone;

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

    /// <summary>
    /// 토핑·조미료 상한은 그 메뉴의 기본 수량 + 3이다.
    /// 시오의 멘마와 쇼유의 차슈는 기본이 2라 5개까지, 기본에 없는 재료는 3개까지 들어간다.
    /// 상한을 정답 레시피에서 뽑으면 상한 자체가 힌트가 되므로, 레시피 책에 이미 공개된
    /// 기본 레시피에서만 뽑는다.
    /// </summary>
    private const int ExtraOverBase = 3;

    // ── 그릇 안 표시 ─────────────────────────────────────────────
    private const string ContentsName = "Contents";

    /// <summary>담긴 재료 그림의 이름 앞머리. 뒤에 재료 종류가 붙는다.</summary>
    private const string IconPrefix = "Icon_";

    // 재료가 국물에 잠긴 표현은 이 클래스가 아니라 빌더가 맡는다.
    // RamenLayoutBuilder.ClipUnderBroth를 켜면 Contents에 Mask가 붙어 수면 아래가 잘린다.
    // 지금은 꺼져 있어서 재료가 통째로 보인다.

    /// <summary>
    /// 그릇을 원본 픽셀의 몇 배로 띄우는지. RamenLayoutBuilder.BowlScale과 같은 값이어야 한다.
    /// 아래 자리표는 전부 원본 픽셀 기준으로 적혀 있고, 여기서 한 번에 곱해 쓴다.
    /// 그래야 크기를 바꿀 때 자리표 24개를 다시 계산하지 않아도 된다.
    /// </summary>
    private const float BowlPixelScale = 2f;

    // 재료 그림 크기는 그림 자신이 정한다. 아래 Layouts에는 크기 값이 없다.
    //
    // 예전에는 64px 원본을 32로 줄여 그렸다. 픽셀아트를 0.5배로 줄이면 한 픽셀 건너
    // 하나씩 버려져 외곽선이 끊기고 파 링의 구멍이 메워졌다. 그래서 "OO 그릇용.png"를
    // 화면에 나올 크기(김 42, 차슈 36, 멘마 34, 계란 32, 숙주·목이버섯 30, 파 24)로
    // 따로 찍어 두고 1:1로 얹는다. 크기를 바꾸려면 그림을 다시 찍어야 한다.

    // 한 재료의 최대 수량 = 기본 레시피 + 추가 3. B의 GetBaseRecipe 최대가
    // 차슈·멘마 2라 최대 5개까지 나온다. 아래 자리표를 그만큼 채워 두고,
    // 넘치면 처음 자리로 돌아가 겹친다.

    /// <summary>몇 번째로 담기느냐에 따라 달라지는 자리와 기울기.</summary>
    private class Placement
    {
        public readonly Vector2 Pos;
        public readonly float Angle;

        /// <summary>
        /// 그림 원본 크기에 곱하는 값. 기본은 1이고 그때가 1:1이라 가장 또렷하다.
        /// 그림을 다시 찍기 전에 크기만 잠깐 보고 싶을 때만 쓴다.
        /// </summary>
        public readonly float Scale;

        public Placement(float x, float y, float angle, float scale = 1f)
        {
            Pos = new Vector2(x, y);
            Angle = angle;
            Scale = scale;
        }
    }

    /// <summary>재료 하나의 배치 규칙.</summary>
    private class ToppingLayout
    {
        /// <summary>
        /// 45도 시점의 앞뒤 순서. 작을수록 뒤에 그려진다.
        /// 앞뒤 두 단계로는 부족하다. 김을 나중에 넣어도 차슈 뒤에 서 있어야 하는데,
        /// 넣은 순서대로 쌓으면 늦게 넣은 김이 차슈를 덮어 버린다.
        /// </summary>
        public readonly int Depth;
        public readonly Placement[] Spots;

        public ToppingLayout(int depth, params Placement[] spots)
        {
            Depth = depth;
            Spots = spots;
        }
    }

    // 그릇 원본(128px)에서 국물 면은 x 20~108, y 47~83이다. 원본 크기로 띄우므로
    // 그릇 중심 기준으로 가로 ±44, 세로 -19~+17이 국물 면이 된다.
    // 자리는 이 안에 두는 것이 원칙이다. 김만 예외로, 테두리에 기대 세운 것이라 위로 솟는다.
    //
    // 실제 라멘 사진의 정석 구성을 따랐다.
    //   김     뒤 왼쪽에 겹겹이 세우고
    //   차슈   왼쪽에 부채처럼 겹쳐 눕히고
    //   계란   뒤 가운데에 자른 면이 보이게
    //   멘마   오른쪽에 비스듬히
    //   숙주   가운데 봉긋하게
    //   목이버섯·파  앞쪽에 흩뿌려 마무리
    //
    // 무엇을 몇 개 담든 재료는 그릇 실루엣 안에 있어야 한다. 밖으로 삐져나오면
    // 허공에 뜬 것처럼 보인다. 그릇 윗변은 가운데가 y 40이고 왼쪽으로 갈수록 낮아져
    // x -35에서 y 34, x -50에서 y 26이다. 자리를 옮길 때는 그 선을 넘지 않게 둘 것.
    //
    // 좌표는 미리보기로 렌더해 가며 잡았다. 바꿀 때는 한 번에 한 재료씩 옮기고
    // 많이 담았을 때(재료별 3~5개) 그릇 밖으로 넘치지 않는지 같이 확인할 것.
    private static readonly Dictionary<IngredientType, ToppingLayout> Layouts =
        new Dictionary<IngredientType, ToppingLayout>
        {
            // 김: 맨 뒤. 오른쪽으로 기울여 겹겹이 세운다.
            // 넓게 펼치면 그릇 밖으로 나가므로 6px씩만 어긋나게 포갠다.
            // 그래도 다섯 장 모두 자기 테두리가 보여 장수가 읽힌다.
            { IngredientType.Nori, new ToppingLayout(0,
                new Placement(-24f, 16f,  11f),
                new Placement(-13f, 20f,   8f),
                new Placement(-31f, 13f,  14f),
                new Placement(-18f, 18f,  10f),
                new Placement( -5f, 21f,   6f)) },

            // 계란: 뒤 가운데에서 오른쪽. 노른자가 보이게 눕힌다
            { IngredientType.Egg, new ToppingLayout(1,
                new Placement( 16f, 12f,  -8f),
                new Placement( 25f,  7f,   6f),
                new Placement(  6f, 15f,   3f)) },

            // 멘마: 오른쪽 끝. 계란과 겹치지 않게 바깥으로 붙인다
            { IngredientType.Menma, new ToppingLayout(2,
                new Placement( 31f,  1f, -22f),
                new Placement( 26f, -7f, -28f),
                new Placement( 33f,  9f, -14f)) },

            // 차슈: 왼쪽에서 아래로 내려가는 대각선.
            // 30도로 눕혀 자른 면이 4~5시 방향을 보게 한다. 일자로 두면 접시에 붙은 것처럼 보인다.
            { IngredientType.Chashu, new ToppingLayout(3,
                new Placement( -7f,  9f,  30f),
                new Placement(-14f,  4f,  30f),
                new Placement(-21f, -1f,  30f),
                new Placement(-24f, -2f,  32f),
                new Placement(  0f, 14f,  28f)) },

            // 숙주: 계란 바로 아래 가운데
            { IngredientType.BeanSprout, new ToppingLayout(4,
                new Placement(  2f,  2f,   0f),
                new Placement( -4f,  6f,   5f),
                new Placement(  8f,  5f,  -6f)) },

            // 목이버섯: 숙주 아래, 가로로 퍼진다
            { IngredientType.WoodEar, new ToppingLayout(5,
                new Placement( -1f, -8f,   0f),
                new Placement(-11f, -10f,  6f),
                new Placement(  8f, -10f, -5f)) },

            // 파: 맨 앞. 오른쪽 앞에만 뭉쳐 놓는다
            { IngredientType.GreenOnion, new ToppingLayout(6,
                new Placement( 18f, -9f,   0f),
                new Placement( 24f, -4f,   0f),
                new Placement( 11f, -12f,  0f),
                new Placement( 28f, -11f,  0f)) },
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

    /// <summary>
    /// 규칙에 맞으면 담고 그림을 얹는다. 어기면 그릇 전체를 붉게 깜빡인다.
    /// 담기에 성공하면 저장을 남긴다. 조리 도중에 꺼도 그릇이 되살아나야 한다(기획서 13.1).
    /// </summary>
    public bool TryAdd(IngredientType type, Sprite icon)
    {
        bool added = AddToBowl(type, icon);

        // 이어하기로 그릇을 되채우는 중에는 남기지 않는다. 복원하면서 저장을 다시 쓰면 헛일이다.
        if (added && !SaveSystem.Restoring && GameManager.Instance != null) GameManager.Instance.SaveNow();

        return added;
    }

    private bool AddToBowl(IngredientType type, Sprite icon)
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

        // 타래와 육수는 그릇 그림을 바로 갈아 끼우지 않고 부어지는 장면을 보여 준다.
        // 면은 한 장짜리라 애니메이션 없이 바로 바뀐다.
        Sprite[] sheet = CurrentSheet;
        if (sheet != null && sheet.Length > NoodleFrame)
        {
            if (IsTare(type)) { PlayPour(sheet, TareFirstFrame, TareLastFrame); return true; }
            if (type == IngredientType.Broth) { PlayPour(sheet, BrothFirstFrame, BrothLastFrame); return true; }
        }

        // 토핑을 올리면 국물이 한 바퀴 찰랑인다. 면까지 들어가야 그릇 그림이 그 상태가 되므로
        // 그 전에 올린 토핑은 찰랑이지 않는다. 면 자체는 담기는 그림이 따로 있어 여기서 뺀다.
        Sprite[] ripple = CurrentRippleSheet;
        if (ripple != null && !IsNoodle(type) && NoodleCount > 0)
        {
            PlayPour(ripple, RippleFirstFrame, RippleLastFrame, rippleFps);
            return true;
        }

        RefreshBowlSprite();
        return true;
    }

    /// <summary>굵은면·얇은면 어느 쪽이든 면인가.</summary>
    private static bool IsNoodle(IngredientType type)
    {
        return type == IngredientType.ThickNoodles || type == IngredientType.ThinNoodles;
    }

    /// <summary>그릇에 든 면의 총 개수. 굵기가 달라도 합쳐서 센다.</summary>
    private int NoodleCount
    {
        get
        {
            return CountIn(bowl, IngredientType.ThickNoodles)
                 + CountIn(bowl, IngredientType.ThinNoodles);
        }
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

        if (IsNoodle(type))
        {
            if (!bowl.ContainsKey(IngredientType.Broth)) return false;

            // 굵은면과 얇은면을 합쳐 한 번만 들어간다. 종류를 바꾸려면 폐기해야 한다.
            return NoodleCount < MaxNoodles;
        }

        // 토핑과 조미료는 순서를 따지지 않는다. 개수만 본다.
        return CountIn(bowl, type) < MaxCountFor(type);
    }

    /// <summary>지금 받아 둔 주문의 메뉴. 주문이 없으면 null이다.</summary>
    private RamenType? CurrentMenu
    {
        get
        {
            if (orderManager == null) orderManager = FindFirstObjectByType<OrderManager>();
            if (orderManager == null) return null;

            // 값을 읽기만 하는 게터라 주문이 새로 만들어지지 않는다.
            CustomerOrder order = orderManager.CurrentOrder;
            return order != null ? order.ramenType : (RamenType?)null;
        }
    }

    /// <summary>이 재료를 몇 개까지 넣을 수 있는가. 기본 레시피 수량 + 3이다.</summary>
    private int MaxCountFor(IngredientType type)
    {
        RamenType? menu = CurrentMenu;

        // 주문 없이 조리해 보는 중이면(디버그 테스터 등) 막을 근거가 없다.
        if (menu == null) return int.MaxValue;

        Dictionary<IngredientType, int> baseRecipe = RecipeGenerator.GetBaseRecipe(menu.Value);
        int baseCount;
        if (!baseRecipe.TryGetValue(type, out baseCount)) baseCount = 0;

        return baseCount + ExtraOverBase;
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
        else if (IsNoodle(type) && !bowl.ContainsKey(IngredientType.Broth)) reason = "육수를 먼저 부어야 합니다.";
        else if (CountIn(bowl, type) >= MaxCountFor(type)) reason = "최대 수량 도달!";
        else reason = "이미 넣었거나 지금 넣을 수 없는 재료입니다.";

        Debug.Log("[투입 거부] " + type + " — " + reason);
        if (toast != null) toast.Show(reason);
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

    /// <summary>
    /// 128칸 그림 안에서 그릇이 실제로 그려지는 자리. (4,24)에서 120 x 80이다.
    /// 빈 그릇도 면까지 담긴 마지막 프레임도 같은 자리라 값 하나로 충분하다.
    ///
    /// 상자는 256인데 그림은 그보다 작다. 상자로 판정하면 아직 안 닿아 보이는데 반응하고,
    /// 그림으로 판정하면 눈에 닿는 순간과 반응하는 순간이 정확히 맞는다.
    /// </summary>
    private static readonly Rect ArtInFrame = new Rect(4f / 128f, 24f / 128f, 120f / 128f, 80f / 128f);

    /// <summary>제출 판정에 쓸 "눈에 보이는 그릇"의 화면 사각형.</summary>
    public Rect VisibleScreenRect()
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);   // 0 좌하, 1 좌상, 2 우상, 3 우하

        float left = corners[0].x, bottom = corners[0].y;
        float w = corners[2].x - left, h = corners[2].y - bottom;

        // ArtInFrame은 그림 좌표(위에서 아래)라 유니티 좌표(아래에서 위)로 뒤집는다.
        return new Rect(left + w * ArtInFrame.x,
                        bottom + h * (1f - ArtInFrame.y - ArtInFrame.height),
                        w * ArtInFrame.width,
                        h * ArtInFrame.height);
    }

    /// <summary>지금 그릇이 제출 영역에 걸쳐 있는가. 1픽셀만 겹쳐도 참이다.</summary>
    private bool TouchingSubmitZone()
    {
        if (submitZone == null) submitZone = FindFirstObjectByType<SubmitZone>();
        if (submitZone == null) return false;

        return VisibleScreenRect().Overlaps(submitZone.ScreenRect(), true);
    }

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

        // 그릇 윗부분이 제출 영역에 닿는 순간 불이 들어온다. 마우스가 아니라 그릇이 기준이다.
        if (submitZone == null) submitZone = FindFirstObjectByType<SubmitZone>();
        if (submitZone != null) submitZone.SetHighlight(TouchingSubmitZone());
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        // 불이 켜져 있었으면 제출한다. 보이는 것과 동작이 어긋나지 않도록 판정 기준을 같이 쓴다.
        bool submit = TouchingSubmitZone();
        if (submitZone != null) submitZone.SetHighlight(false);
        if (submit) Submit();

        // 제출됐든 아니든 그릇은 원래 자리로 돌아간다.
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

        // 비운 그릇과 늘어난 폐기 기록을 남긴다. 폐기는 제출할 때 함께 넘어간다.
        if (GameManager.Instance != null) GameManager.Instance.SaveNow();
    }

    // ── 표시와 도우미 ────────────────────────────────────────────

    /// <summary>
    /// 지금 그릇이 쓰는 시트. 어떤 타래가 들어갔는지로 정해진다.
    /// 타래가 없으면 아직 시트를 고를 수 없다.
    /// </summary>
    private Sprite[] CurrentSheet
    {
        get
        {
            if (bowl.ContainsKey(IngredientType.ShioTare)) return shioFrames;
            if (bowl.ContainsKey(IngredientType.ShoyuTare)) return shoyuFrames;
            if (bowl.ContainsKey(IngredientType.TonkotsuBase)) return tonkotsuFrames;
            return null;
        }
    }

    /// <summary>
    /// 지금 그릇이 쓰는 찰랑임 시트. 붓기 시트와 같은 기준으로 고른다.
    /// 시트가 짧으면(빌더가 못 잘랐거나 그림이 바뀌었으면) 없는 셈 친다.
    /// </summary>
    private Sprite[] CurrentRippleSheet
    {
        get
        {
            Sprite[] sheet = null;
            if (bowl.ContainsKey(IngredientType.ShioTare)) sheet = shioToppingFrames;
            else if (bowl.ContainsKey(IngredientType.ShoyuTare)) sheet = shoyuToppingFrames;
            else if (bowl.ContainsKey(IngredientType.TonkotsuBase)) sheet = tonkotsuToppingFrames;

            if (sheet == null || sheet.Length <= RippleLastFrame) return null;
            return sheet;
        }
    }

    /// <summary>시트의 한 구간을 재생하고 지금 상태에 맞는 그림으로 안착한다.</summary>
    private void PlayPour(Sprite[] frames, int first, int last)
    {
        PlayPour(frames, first, last, pourFps);
    }

    private void PlayPour(Sprite[] frames, int first, int last, float fps)
    {
        StopBrothPour();
        brothPour = StartCoroutine(PourRoutine(frames, first, last, fps));
    }

    private void StopBrothPour()
    {
        if (brothPour != null)
        {
            StopCoroutine(brothPour);
            brothPour = null;
        }
    }

    private IEnumerator PourRoutine(Sprite[] frames, int first, int last, float fps)
    {
        // fps가 0이나 음수면 아예 안 넘어가므로 최소값을 둔다.
        float perFrame = 1f / Mathf.Max(0.1f, fps);
        int end = Mathf.Min(last, frames.Length - 1);

        for (int i = first; i <= end; i++)
        {
            if (frames[i] != null) image.sprite = frames[i];
            yield return new WaitForSeconds(perFrame);
        }

        RefreshBowlSprite();
        brothPour = null;
    }

    /// <summary>
    /// 지금 담긴 것에 맞는 그릇 그림을 고른다.
    /// 타래가 정해져야 시트가 정해지므로, 타래 전에는 빈 그릇 그림을 쓴다.
    /// </summary>
    private void RefreshBowlSprite()
    {
        Sprite[] sheet = CurrentSheet;
        if (sheet == null || sheet.Length <= NoodleFrame)
        {
            if (emptyBowlSprite != null) image.sprite = emptyBowlSprite;
            return;
        }

        int frame = TareLastFrame;
        if (NoodleCount > 0) frame = NoodleFrame;
        else if (bowl.ContainsKey(IngredientType.Broth)) frame = BrothLastFrame;

        if (sheet[frame] != null) image.sprite = sheet[frame];
    }

    private void AddIcon(IngredientType type, Sprite icon, int count)
    {
        if (contents == null) return;

        // 타래·육수·면은 그릇 그림 자체가 바뀌므로 따로 얹을 게 없다.
        // 면은 시트 마지막 프레임에 이미 그려져 있어서, 따로 얹으면 두 번 겹친다.
        // 굵은면과 얇은면은 그릇 안에서 같은 그림을 쓴다.
        if (IsTare(type) || type == IngredientType.Broth || IsNoodle(type)) return;

        // 조미료는 그릇용 그림이 없다. 수량만 세고 화면에는 안 나온다.
        if (icon == null) return;

        if (!Layouts.TryGetValue(type, out ToppingLayout layout))
        {
            Debug.LogWarning("[Bowl] 그릇 안 자리가 정해지지 않은 재료입니다: " + type);
            return;
        }

        int nth = count - 1;
        Placement spot = layout.Spots[nth % layout.Spots.Length];

        // 자리표를 한 바퀴 다 쓰면 같은 자리에 정확히 겹쳐 넣은 티가 안 난다.
        // 바퀴마다 조금씩 밀고 돌려 쌓인 것처럼 보이게 한다.
        int lap = nth / layout.Spots.Length;
        Vector2 pos = (spot.Pos + new Vector2(2f, -2f) * lap) * BowlPixelScale;
        float angle = spot.Angle + 5f * lap;

        // 그릇용 그림은 그릇을 1배로 띄웠을 때의 크기로 그려져 있다.
        // 그릇이 2배면 재료도 2배여야 둘이 따로 놀지 않는다.
        float side = Mathf.Round(icon.rect.width * spot.Scale) * BowlPixelScale;

        RectTransform placed = CreateIcon(type, icon, pos, new Vector2(side, side), angle);
        InsertByDepth(placed, layout.Depth);
    }

    /// <summary>
    /// 깊이 순서에 맞는 자리에 끼워 넣는다. 나보다 앞에 그려질 것 중 첫 번째 앞으로 간다.
    /// 넣은 순서와 상관없이 김이 늘 맨 뒤, 파가 늘 맨 앞에 오게 하려면 이게 필요하다.
    /// </summary>
    private void InsertByDepth(RectTransform placed, int depth)
    {
        for (int i = 0; i < contents.childCount; i++)
        {
            Transform child = contents.GetChild(i);
            if (child == placed) continue;

            if (DepthOf(child) > depth)
            {
                placed.SetSiblingIndex(i);
                return;
            }
        }
    }

    /// <summary>이미 담겨 있는 그림의 깊이. 이름에 재료 종류가 들어 있다.</summary>
    private static int DepthOf(Transform icon)
    {
        if (!icon.name.StartsWith(IconPrefix)) return 0;

        if (!System.Enum.TryParse(icon.name.Substring(IconPrefix.Length), out IngredientType type)) return 0;

        return Layouts.TryGetValue(type, out ToppingLayout layout) ? layout.Depth : 0;
    }

    private RectTransform CreateIcon(IngredientType type, Sprite icon, Vector2 pos, Vector2 size, float angle)
    {
        var go = new GameObject(IconPrefix + type, typeof(RectTransform), typeof(Image));
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
