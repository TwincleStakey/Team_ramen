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
public class Bowl : MonoBehaviour, IDropHandler, IPointerClickHandler
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

    /// <summary>
    /// 그릇에 얹는 재료 그림 30칸(토핑배치.png). 자리마다 기울기와 국물에 잠긴 깊이가
    /// 이미 구워져 있어서, 런타임에는 Layouts 가 가리키는 칸을 골라 놓기만 한다.
    /// 칸 순서는 Layouts 의 SheetStart 와 맞춰야 한다.
    /// </summary>
    public Sprite[] toppingFrames;

    /// <summary>구운 칸 한 변(원본 픽셀). 회전한 그림 중 가장 큰 것에 맞춰 잡았다.</summary>
    private const float ToppingCellSize = 50f;

    /// <summary>투입을 거부했을 때 이유를 띄우는 안내. RamenLayoutBuilder가 꽂아 준다.</summary>
    public IngredientToast toast;

    /// <summary>
    /// 시치미·향미유 개수 배지. 마찬가지로 빌더가 꽂아 준다.
    /// 이 둘은 아래 AddIcon 이 조미료로 보고 걸러서 그릇 그림이 그대로다. 그래서 따로 센다.
    /// </summary>
    public SeasoningBadges badges;

    /// <summary>
    /// 붓기 애니메이션 길이(초). 장수가 달라도 이 시간 안에 다 돈다.
    ///
    /// 국자가 기울어지는 동안 그릇이 꼭 맞게 차오르도록 국자 쪽 길이를 그대로 쓴다.
    /// 타래는 4장, 육수는 3장이라 초당 장수로 맞추면 둘의 길이가 어긋난다.
    /// </summary>
    private const float PourSeconds = CookingCursor.LadlePourSeconds;

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

    /// <summary>
    /// 토핑을 얹었을 때 국물이 한 바퀴 찰랑이는 연출. 도는 중에만 값이 들어 있고,
    /// 다 돌면 스스로 null 로 돌아간다. 쉬는 동안에는 찰랑임 시트의 첫 장이 그대로 서 있다.
    /// </summary>
    private Coroutine rippleLoop;

    /// <summary>상한을 정하려면 지금 손님이 시킨 메뉴를 알아야 한다. 처음 쓸 때 한 번 찾아 둔다.</summary>
    private OrderManager orderManager;

    // 지금 그릇에 담긴 재료
    private readonly Dictionary<IngredientType, int> bowl = new Dictionary<IngredientType, int>();

    // 폐기 버튼으로 버린 재료의 누적. 제출할 때 함께 넘긴다.
    private readonly Dictionary<IngredientType, int> discarded = new Dictionary<IngredientType, int>();

    // ── 투입 규칙 ────────────────────────────────────────────────
    // 타래 3종은 합쳐서 1개. 하나라도 들어 있으면 전부 막으므로 "1회 제한"과 "교체 불가"가 동시에 걸린다.
    // 타래는 육수가 먼저 들어가야 넣을 수 있다. 빈 그릇에 타래만 붓는 상태를 만들지 않기 위해서다.
    private const int MaxBroth = 1;
    private const int MaxNoodles = 1;   // RecipeGenerator도 면을 항상 1로 둔다

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

        /// <summary>
        /// 이 자리의 기울기. 런타임에는 쓰지 않는다 — 토핑배치.png에 이미 구워 넣었다.
        /// 시트를 다시 구울 때 쓰는 값이라 여기 남겨 둔다. 이 값을 고치면 시트도 다시 구워야 한다.
        /// </summary>
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
        /// <summary>
        /// 45도 시점의 앞뒤 순서. 작을수록 뒤에 그려진다.
        /// 앞뒤 두 단계로는 부족하다. 김을 나중에 넣어도 차슈 뒤에 서 있어야 하는데,
        /// 넣은 순서대로 쌓으면 늦게 넣은 김이 차슈를 덮어 버린다.
        /// </summary>
        public readonly int Depth;

        /// <summary>이 재료의 첫 자리가 토핑배치.png 에서 몇 번째 칸인지.</summary>
        public readonly int SheetStart;

        public readonly Placement[] Spots;

        public ToppingLayout(int depth, int sheetStart, params Placement[] spots)
        {
            Depth = depth;
            SheetStart = sheetStart;
            Spots = spots;
        }
    }

    // 자리는 미리보기 렌더러로 그려 가며 잡았다. 좌표는 그릇 원본(128px) 기준이고
    // 그릇 한가운데가 (0,0), y는 위가 +다.
    //
    // 경계는 두 겹이다.
    //   하드  그릇 실루엣을 5칸 안으로 민 선. 넘으면 그릇 밖 허공에 뜬 것처럼 보인다.
    //         김만 예외로 위쪽으로 솟는다. 실제 라멘도 김은 테두리 위로 삐져나온다.
    //   소프트 국물 면 타원(중심 0,+2 / 반지름 47 x 21.5). 떠 있는 파·숙주·목이버섯이 지킨다.
    //
    // 자리 수는 재료별 상한 이상이다. 그래서 정상 플레이에서는 아래 lap 이 한 번도 돌지 않는다.
    // lap 은 주문 없이 조리하는 디버그용 안전망이다.
    //
    // 상한은 전 재료 4다(RecipeGenerator.MAX_TOPPING_COUNT). 멘마·차슈만 자리가 5개인데,
    // 상한이 "그 메뉴 기본 수량 + 3"이던 시절에 잡아 둔 것이라 5번째 자리는 이제 안 쓰인다.
    // 김은 원래 3개였다. 돈코츠 기본에 김 1회가 들어오면서(기획서 v1.2 4.1) 정답이 4까지
    // 나올 수 있게 되어 한 자리를 늘렸다. 자리를 늘리면 뒤따르는 SheetStart 가 전부 밀린다.
    //
    // 실제 라멘 사진의 정석 구성을 따랐다.
    //   김     뒤 왼쪽에 세워 테두리 위로 솟게
    //   차슈   왼쪽 허리에 카드처럼 겹쳐 눕히고
    //   계란   뒤 오른쪽에 넓게 펴서 자른 면이 보이게
    //   멘마   오른쪽 바깥에 비스듬히
    //   숙주   가운데 봉긋하게
    //   목이버섯·파  앞쪽에 흩뿌려 마무리
    //
    // 자리마다 기울기와 국물에 잠기는 깊이가 다른데, 둘 다 토핑배치.png 에 구워 넣었다.
    // 런타임에는 회전도 자르기도 하지 않고 칸을 골라 놓기만 한다. 회전을 코드로 하면
    // 픽셀아트가 반칸에 걸려 뭉개지고, 잠긴 부분을 색으로 덮으면 뒤에 있는 재료와
    // 국물 찰랑임을 같이 가려 버린다. 구울 때 아예 지우면 그 자리에 그릇 국물이 그대로 비친다.
    //
    // 좌표를 고치면 시트를 다시 구워야 한다. 한 번에 한 재료씩 옮기고, 그 재료를 상한까지
    // 담았을 때 그릇 밖으로 넘치지 않는지 같이 확인할 것.
    private static readonly Dictionary<IngredientType, ToppingLayout> Layouts =
        new Dictionary<IngredientType, ToppingLayout>
        {
            // 김: 맨 뒤. 뒤 왼쪽에 세우고 오른쪽으로 조금씩 어긋나게 포갠다.
            // 밑동은 국물에 잠기고 윗부분이 테두리 위로 솟는다.
            { IngredientType.Nori, new ToppingLayout(0, 0,
                new Placement(-26f, 24f,  12f),
                new Placement(-18f, 25f,   9f),
                new Placement(-10f, 26f,   5f),
                new Placement( -2f, 27f,   2f)) },

            // 계란: 뒤 오른쪽. 넓게 펴야 개수가 읽힌다. 좁게 두면 넷이 둘로 보인다.
            { IngredientType.Egg, new ToppingLayout(1, 4,
                new Placement(  9f, 17f, -10f),
                new Placement( 21f, 15f,  -4f),
                new Placement( 31f, 10f,   3f),
                new Placement( 26f,  1f, -12f)) },

            // 멘마: 오른쪽 끝. 계란 아래로 비스듬히 세운다
            { IngredientType.Menma, new ToppingLayout(2, 8,
                new Placement( 33f,  4f, -18f),
                new Placement( 28f, -1f, -24f),
                new Placement( 34f,  9f, -12f),
                new Placement( 24f, -4f, -28f),
                new Placement( 30f, 13f,  -8f)) },

            // 차슈: 왼쪽 허리에서 오른쪽 아래로 완만하게. 앞으로 더 내리면 그릇이 좁아져 안 들어간다.
            { IngredientType.Chashu, new ToppingLayout(3, 13,
                new Placement(-24f,  7f,  28f),
                new Placement(-18f,  5f,  29f),
                new Placement(-12f,  3f,  30f),
                new Placement( -6f,  1f,  31f),
                new Placement(  0f, -1f,  32f)) },

            // 숙주: 가운데
            { IngredientType.BeanSprout, new ToppingLayout(4, 18,
                new Placement(  0f,  5f,   0f),
                new Placement( -7f,  8f,   4f),
                new Placement(  7f,  7f,  -4f),
                new Placement(  0f, 11f,   2f)) },

            // 목이버섯: 숙주 앞, 가로로 퍼진다
            { IngredientType.WoodEar, new ToppingLayout(5, 22,
                new Placement( -4f, -5f,   0f),
                new Placement(  6f, -7f,  -5f),
                new Placement(-12f, -7f,   5f),
                new Placement( 13f, -4f,  -8f)) },

            // 파: 맨 앞. 국물 위에 거의 떠 있다
            { IngredientType.GreenOnion, new ToppingLayout(6, 26,
                new Placement( 15f, -7f,   0f),
                new Placement( 22f, -3f,   0f),
                new Placement(  9f,-10f,   0f),
                new Placement( 18f,-10f,   0f)) },
        };

    // ── 거부 표시 ────────────────────────────────────────────────
    private static readonly Color RejectColor = new Color(1f, 0.45f, 0.4f);
    private const float BlinkSeconds = 0.18f;

    private RectTransform rect;
    private Image image;
    private Canvas canvas;
    private Transform contents;
    private Coroutine blink;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        canvas = GetComponentInParent<Canvas>();
        contents = transform.Find(ContentsName);

        RefreshBowlSprite();

        if (contents == null)
        {
            Debug.LogWarning("[Bowl] Contents 자식을 찾지 못했습니다. Tools > Ramen > Build Cooking Layout을 다시 실행해 주세요.");
        }
    }

    // ── 재료 받기 ────────────────────────────────────────────────

    /// <summary>
    /// 재료통에서 끌어온 것을 놓았을 때. uGUI가 슬롯의 OnEndDrag보다 먼저 부른다.
    ///
    /// 고체는 슬롯이 무엇인지 들고 있고, 액체·조미료는 커서가 들고 있다.
    /// 국자로 뜬 것을 도로 통에 놓았다가 끌어와도 되므로, 슬롯이 아니라 커서에게 묻는다.
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        var solid = eventData.pointerDrag.GetComponent<IngredientSlot>();
        if (solid != null)
        {
            TryAdd(solid.type, solid.bowlSprite);
            return;
        }

        // 면 소쿠리. 국자·병과 판정 규칙이 같아서 아래 흐름을 그대로 탄다.
        var noodle = eventData.pointerDrag.GetComponent<NoodleSlot>();
        if (noodle != null)
        {
            CookingCursor holder = CookingCursor.Instance;
            if (holder == null || !holder.IsHolding) return;

            noodle.MarkDelivered();

            IngredientType noodleType = holder.Held;
            holder.Deliver(() => TryAdd(noodleType, null));
            return;
        }

        var liquid = eventData.pointerDrag.GetComponent<LiquidSlot>();
        if (liquid == null) return;

        // 뜨는 동작이 아직 안 끝났으면 국자가 비어 있다. 그때는 아무것도 부어지지 않는다.
        CookingCursor cursor = CookingCursor.Instance;
        if (cursor == null || !cursor.IsHolding) return;

        // 통 쪽이 또 내려놓지 않게 먼저 알린다.
        // 병은 이제 뿌리는 동작에 들어가는데, 끊기면 자세가 기운 채로 멈춘다.
        liquid.MarkDelivered();

        // 넣어지는 판정은 동작이 다 끝난 뒤다. 병은 다 뿌린 다음, 국자는 이미 다 퍼 왔으니 곧바로.
        IngredientType held = cursor.Held;
        cursor.Deliver(() => TryAdd(held, null));
    }

    /// <summary>
    /// 국자나 병을 든 채로 그릇을 클릭했을 때.
    ///
    /// 평소에는 올 일이 없다. 통에서 끌어다 놓는 것이 정식 손놀림이고, 끌기가 끝나면
    /// 커서가 빈손으로 돌아가기 때문이다. 디버그로 커서에 직접 들려 놓은 경우를 위해 남겨 둔다.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        CookingCursor cursor = CookingCursor.Instance;
        if (cursor == null || !cursor.IsHolding) return;

        IngredientType held = cursor.Held;
        cursor.Deliver(() => TryAdd(held, null));
    }

    /// <summary>
    /// 규칙에 맞으면 담고 그림을 얹는다. 어기면 그릇 전체를 붉게 깜빡인다.
    /// 담기에 성공하면 저장을 남긴다. 조리 도중에 꺼도 그릇이 되살아나야 한다(기획서 13.1).
    /// </summary>
    public bool TryAdd(IngredientType type, Sprite icon)
    {
        bool added = AddToBowl(type, icon);

        // 튜토리얼은 실제로 들어간 뒤에만 다음 차례로 넘어간다.
        // 거부당한 투입으로 차례가 밀리면 안내가 그릇 상태와 어긋난다.
        if (added && TutorialManager.Instance != null) TutorialManager.Instance.NotifyAdded(type);

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
        RefreshBadges();

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
            // 처음부터 다시 돌린다. 기름이 가운데에서 확 퍼지는 장이 첫머리라 그게 곧 "얹은" 표시가 된다.
            StartRipple(ripple);
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
        // 튜토리얼에서는 안내한 재료만 받는다.
        //
        // 통 쪽에서도 막지만 여기가 마지막 관문이다. 재료가 그릇에 닿는 길은 통 셋 말고도
        // 커서에 들린 채 그릇을 클릭하는 길과 디버그 테스터가 있어서, 입구마다 따로 막으면
        // 하나씩 새기 쉽다. 실제로 통에서 OnPointerDown 만 막았더니 OnBeginDrag 로 다 들어왔다.
        if (!TutorialManager.CanPick(type)) return false;

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

    /// <summary>
    /// 이 재료를 몇 개까지 넣을 수 있는가. 기획서 v1.2 3.2·10.1·11.3 — 재료를 가리지 않고 4다.
    ///
    /// 정답 수량의 상한(<see cref="RecipeGenerator.MAX_TOPPING_COUNT"/>)을 그대로 가져다 쓴다.
    /// 여기에 4를 따로 적어 두면 두 곳이 말없이 어긋난다. 실제로 예전에 이 상한이
    /// "기본 수량 + 3"이라 기본 2인 멘마·차슈가 5개까지 들어갔고, 정답은 4가 최대라
    /// 5개째는 반드시 틀리는 헛투입이 됐다.
    /// </summary>
    private int MaxCountFor(IngredientType type)
    {
        return RecipeGenerator.MAX_TOPPING_COUNT;
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

    /// <summary>아무것도 안 들어간 그릇인가. 폐기 확인창이 물어볼 것이 있는지 판단할 때 쓴다.</summary>
    public bool IsEmpty => bowl.Count == 0;

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

        // 버리는 순간 화면이 한 번 거칠어지고 흔들린다. 그릇이 그냥 비워지기만 하면
        // 방금 한 그릇을 통째로 날렸다는 게 손에 안 남는다.
        if (ScreenGrain.Instance != null) ScreenGrain.Instance.Flash();

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

    /// <summary>시트의 한 구간을 PourSeconds 동안 재생하고 지금 상태에 맞는 그림으로 안착한다.</summary>
    private void PlayPour(Sprite[] frames, int first, int last)
    {
        int count = Mathf.Max(1, Mathf.Min(last, frames.Length - 1) - first + 1);
        PlayPour(frames, first, last, count / PourSeconds);
    }

    private void PlayPour(Sprite[] frames, int first, int last, float fps)
    {
        StopRipple();
        StopBrothPour();
        brothPour = StartCoroutine(PourRoutine(frames, first, last, fps));
    }

    /// <summary>
    /// 국물 찰랑임을 한 바퀴 돌린다. 움직이는 것은 국물 위에 뜬 기름 띠뿐이고 나머지는 고정이다.
    /// 다 돌면 첫 장에 서서 멈춘다.
    ///
    /// 한때 끝없이 돌렸다. 멈춰 있으면 기름이 굳어 사진처럼 보인다는 이유였는데,
    /// 계속 도는 쪽은 두 가지가 나빴다. 아무것도 안 했는데 그릇이 늘 움직여 눈이 끌리고,
    /// 코루틴이 매 프레임 그릇 그림을 덮어써서 폐기해도 빈 그릇으로 안 바뀌었다.
    /// </summary>
    private void StartRipple(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0) return;

        if (rippleLoop != null) StopCoroutine(rippleLoop);

        StopBrothPour();
        rippleLoop = StartCoroutine(RippleRoutine(frames));
    }

    private void StopRipple()
    {
        if (rippleLoop == null) return;

        StopCoroutine(rippleLoop);
        rippleLoop = null;
    }

    private IEnumerator RippleRoutine(Sprite[] frames)
    {
        float perFrame = 1f / Mathf.Max(0.1f, rippleFps);
        int end = Mathf.Min(RippleLastFrame, frames.Length - 1);

        for (int i = RippleFirstFrame; i <= end; i++)
        {
            if (frames[i] != null) image.sprite = frames[i];
            yield return new WaitForSeconds(perFrame);
        }

        // 0번과 15번이 같은 그림이라 여기서 서면 첫 장에 선 것과 같다.
        if (frames[RippleFirstFrame] != null) image.sprite = frames[RippleFirstFrame];
        rippleLoop = null;
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
            // 찰랑임을 먼저 끊는다. 예전에는 이 분기가 그냥 빠져나가서, 폐기한 뒤에도
            // 돌고 있던 찰랑임이 다음 프레임에 그릇 그림을 도로 덮어썼다.
            // 빈 그릇으로 안 바뀌던 것이 이것 때문이다.
            StopRipple();
            if (emptyBowlSprite != null) image.sprite = emptyBowlSprite;
            return;
        }

        // 면까지 들어갔으면 붓기 시트가 아니라 찰랑임 시트의 첫 장에 안착한다.
        //
        // 붓기 시트의 면 프레임은 국물이 그릇 중턱까지만 차 있고, 찰랑임 시트는 턱 가까이 차 있다.
        // 그대로 두면 토핑을 얹어 찰랑임이 도는 순간 수면이 껑충 뛰어 층이 진다.
        // 쉬는 그림을 찰랑임 0번으로 맞춰 두면 그 층이 아예 안 생긴다.
        //
        // 기름만 떼어다 붓기 프레임에 얹는 것도 해 봤는데, 기름 띠가 높은 수면 기준으로
        // 그려져 있어서 낮은 국물 위에서는 자리가 안 맞았다.
        if (NoodleCount > 0)
        {
            Sprite[] ripple = CurrentRippleSheet;
            if (ripple != null && ripple[RippleFirstFrame] != null)
            {
                // 한 바퀴 도는 중이면 건드리지 않는다. 다 돌면 스스로 첫 장에 선다.
                // 쉬고 있을 때는 돌리지 않고 첫 장을 그대로 얹기만 한다.
                if (rippleLoop == null) image.sprite = ripple[RippleFirstFrame];
                return;
            }
        }

        // 면이 빠졌으면 찰랑임을 멈춘다. 안 멈추면 아래에서 고른 그림을 계속 덮어쓴다.
        StopRipple();

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
        // 바퀴마다 조금씩 밀어 쌓인 것처럼 보이게 한다.
        //
        // 자리 수를 재료별 상한과 같게 맞춰 두어서, 주문을 받고 조리하는 동안에는 여기까지 오지 않는다.
        // 주문 없이 조리하는 디버그(상한이 없다)에서만 도는 안전망이다.
        int lap = nth / layout.Spots.Length;
        Vector2 pos = (spot.Pos + new Vector2(2f, -2f) * lap) * BowlPixelScale;

        Sprite baked = BakedSprite(layout, nth);
        if (baked == null) return;

        // 구운 칸은 전부 같은 크기다. 기울인 그림이 잘리지 않게 잡은 값이라 여백이 들어 있고,
        // 칸 한가운데가 곧 자리다. 그릇이 2배로 떠 있으므로 재료도 2배여야 따로 놀지 않는다.
        float side = ToppingCellSize * BowlPixelScale;

        RectTransform placed = CreateIcon(type, baked, pos, new Vector2(side, side), 0f);
        InsertByDepth(placed, layout.Depth);
    }

    /// <summary>이 재료의 nth 번째 자리에 해당하는 구운 그림.</summary>
    private Sprite BakedSprite(ToppingLayout layout, int nth)
    {
        if (toppingFrames == null || toppingFrames.Length == 0)
        {
            Debug.LogWarning("[Bowl] 토핑배치 시트가 꽂혀 있지 않습니다. 빌더를 다시 실행해 주세요.");
            return null;
        }

        int index = layout.SheetStart + nth % layout.Spots.Length;
        if (index < 0 || index >= toppingFrames.Length)
        {
            Debug.LogWarning("[Bowl] 토핑배치 시트에 " + index + "번 칸이 없습니다.");
            return null;
        }

        return toppingFrames[index];
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

        // 그릇이 비었으니 튜토리얼 차례도 처음으로 돌아간다.
        if (TutorialManager.Instance != null) TutorialManager.Instance.NotifyBowlCleared();

        RefreshBowlSprite();
        RefreshBadges();
        image.color = Color.white;

        if (contents == null) return;

        for (int i = contents.childCount - 1; i >= 0; i--)
        {
            Destroy(contents.GetChild(i).gameObject);
        }
    }

    /// <summary>그릇 옆 시치미·향미유 개수를 지금 담긴 것에 맞춘다.</summary>
    private void RefreshBadges()
    {
        if (badges == null) return;

        badges.Refresh(CountIn(bowl, IngredientType.FlavorOil),
                       CountIn(bowl, IngredientType.ChiliPowder));
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
