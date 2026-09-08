using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// 조리 화면 UI 계층을 코드로 생성한다.
/// 손으로 배치하면 재현이 안 되므로 좌표를 전부 여기에 상수로 둔다.
/// 메뉴를 다시 누르면 기존 CookingCanvas를 지우고 새로 만든다.
/// 배치 기준은 기획서 14-3 와이어프레임, 그림은 Assets/Art의 45도 픽셀아트.
///
/// 크기는 전부 원본 픽셀의 정수 배율로만 잡는다. 픽셀아트를 1.5배 같은 배율로 늘리면
/// 픽셀 크기가 들쭉날쭉해져 그림이 지저분해진다.
/// </summary>
public static class RamenLayoutBuilder
{
    private const string CanvasName = "CookingCanvas";
    private const string FontPath = "Assets/Fonts/ThinMulmaru Mono.ttf";
    private const string UndoLabel = "Build Cooking Layout";

    private const string BowlDir = "Assets/Art/그릇/";
    private const string EtcDir = "Assets/Art/나머지/";
    private const string IngredientDir = "Assets/Art/재료/";

    private static readonly Vector2 RefResolution = new Vector2(1920f, 1080f);

    // 앵커 프리셋. anchorMin과 anchorMax를 같은 값으로 두면 그 지점이 좌표의 원점이 된다.
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);
    private static readonly Vector2 MidLeft = new Vector2(0f, 0.5f);
    private static readonly Vector2 MidRight = new Vector2(1f, 0.5f);
    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 BottomRight = new Vector2(1f, 0f);

    // 원본 픽셀의 몇 배로 띄울지를 실제 물건 크기 느낌에 맞춰 정한 것.
    // 냄비 > 재료통·타래통 > 조미료병 순서고, 면 튀김기가 제일 큰 장비다.
    // 정산 팝업은 밝은 판 위에 글자를 얹으므로 조리 화면의 흰 글자를 그대로 쓸 수 없다.
    private static readonly Color PopupInkColor = new Color(0.16f, 0.16f, 0.16f);
    private const string TmpFontPath = "Assets/Fonts/ThinMulmaru Mono SDF.asset";

    private static readonly Vector2 BinSize = new Vector2(192f, 192f);        // 64px x 3
    private static readonly Vector2 PotSize = new Vector2(320f, 320f);        // 64px x 5 (육수 냄비)
    private static readonly Vector2 NoodleBinSize = new Vector2(256f, 256f);  // 128px x 2. 두 종류를 나란히 두려고 줄였다
    private static readonly Vector2 BowlSize = new Vector2(768f, 768f);       // 128px x 6
    private static readonly Vector2 CursorSize = new Vector2(256f, 256f);     // 64px x 4

    // 글자는 흰색으로 두고 검은 테두리를 둘러 배경 위에서 읽히게 한다.
    // 배경이 베이지 판과 45도 픽셀아트로 갈려서 단색 글자로는 한쪽에서 반드시 묻힌다.
    private static readonly Color InkColor = Color.white;
    private static readonly Color TextOutlineColor = Color.black;
    private static readonly Vector2 TextOutlineDistance = new Vector2(2f, -2f);
    private const int SlotLabelSize = 25;

    /// <summary>상단 바에서 만들어 두고 나중에 다른 것과 연결해야 하는 것들.</summary>
    private class TopBarRefs
    {
        public Image Discard;      // 그릇이 생긴 뒤 onClick을 붙인다
        public Image Help;         // ? 버튼. 눌리면 주문 화면을 다시 연다
        public Text DayText;       // GameManager가 일차를 써 넣는다
        public Text RevenueText;   // GameManager가 누적 매출을 써 넣는다
    }

    /// <summary>주문 시스템 오브젝트에서 나중에 다른 것과 연결해야 하는 것들.</summary>
    private class OrderSystemRefs
    {
        public OrderManager Order;   // 주문 생성과 채점
        public DayManager Day;       // 일차·손님 수 진행
    }

    /// <summary>손님별 결과창에서 OrderResultUI에 꽂아 줘야 하는 것들.</summary>
    private class OrderResultRefs
    {
        public GameObject Root;
        public TextMeshProUGUI Accuracy;
        public TextMeshProUGUI Reward;
        public TextMeshProUGUI Revenue;
        public TextMeshProUGUI CustomerLine;
        public Image Emoji;
        public Sprite[] Faces;
        public Button Confirm;
    }

    /// <summary>정보 패널(? 버튼)에서 RecipeBookUI에 꽂아 줘야 하는 것들.</summary>
    private class RecipeBookRefs
    {
        public GameObject Root;
        public TextMeshProUGUI Order;
        public TextMeshProUGUI RecipeNames;
        public TextMeshProUGUI RecipeValues;
        public TextMeshProUGUI IngredientNames;
        public TextMeshProUGUI IngredientAttrs;
        public Button Close;
    }

    /// <summary>주문 화면에서 OrderScreenUI에 꽂아 줘야 하는 것들.</summary>
    private class OrderScreenRefs
    {
        public GameObject Root;
        public TextMeshProUGUI DayTime;
        public TextMeshProUGUI Revenue;
        public TextMeshProUGUI Dialogue;
        public Button Start;
    }

    /// <summary>5일 완료 화면에서 FinalResultUI에 꽂아 줘야 하는 것들.</summary>
    private class FinalPopupRefs
    {
        public GameObject Root;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Revenue;
        public TextMeshProUGUI Accuracy;
        public TextMeshProUGUI Perfect;
        public Button Restart;
    }

    /// <summary>정산 팝업에서 B의 DailyResultUI에 꽂아 줘야 하는 것들.</summary>
    private class ResultPopupRefs
    {
        public GameObject Root;              // 열고 닫을 때 통째로 켜고 끈다
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Profit;
        public TextMeshProUGUI Accuracy;
        public Button Confirm;
    }

    /// <summary>슬롯 하나의 정의. 좌표표를 그대로 코드로 옮긴 것.</summary>
    private class SlotDef
    {
        public readonly IngredientType Type;
        public readonly string Label;
        public readonly Vector2 Anchor;
        public readonly Vector2 Pos;
        public readonly Vector2 Size;

        /// <summary>화면에 놓이는 통 그림.</summary>
        public readonly string BinPath;

        /// <summary>그릇 안에 얹히는 그림. 액체와 조미료는 없어서 비어 있다.</summary>
        public readonly string BowlPath;

        /// <summary>드래그할 때 따라다니는 그림. 없으면 BowlPath를 쓴다.</summary>
        public readonly string DragPath;

        /// <summary>참이면 끌 수 없고, 클릭해서 국자에 담는다.</summary>
        public readonly bool Liquid;

        /// <summary>이름표를 통 그림 아래가 아닌 다른 곳에 둘 때 쓴다. (0,0)이면 기본 위치.</summary>
        public readonly Vector2 LabelOffset;

        /// <summary>
        /// 오브젝트 이름 뒤에 붙는 꼬리표. 굵은면·얇은면처럼 한 IngredientType으로
        /// 슬롯을 둘 만들 때 이름이 겹치지 않게 한다.
        /// </summary>
        public readonly string IdSuffix;

        public SlotDef(IngredientType type, string label, Vector2 anchor, float x, float y, Vector2 size,
                       string binPath, string bowlPath = null, string dragPath = null, bool liquid = false,
                       float labelX = 0f, float labelY = 0f, string idSuffix = null)
        {
            IdSuffix = idSuffix;
            Type = type;
            Label = label;
            Anchor = anchor;
            Pos = new Vector2(x, y);
            Size = size;
            BinPath = binPath;
            BowlPath = bowlPath;
            DragPath = dragPath;
            Liquid = liquid;
            LabelOffset = new Vector2(labelX, labelY);
        }
    }

    private static readonly SlotDef[] Slots =
    {
        // 좌상단: 타래 3통을 세로 한 줄로. 와이어프레임 기준이다. 이름표는 통 왼쪽.
        // 클릭하면 커서가 국자로 바뀌며 뜬다. 통 높이 192에 간격 160이라 32씩 겹치지만
        // 그림에 여백이 있어 눈에는 안 겹쳐 보인다.
        new SlotDef(IngredientType.ShioTare,     "시오",     TopLeft, 330f, -230f, BinSize,
                    EtcDir + "시오.png", liquid: true, labelX: -190f),
        new SlotDef(IngredientType.ShoyuTare,    "쇼유",     TopLeft, 330f, -390f, BinSize,
                    EtcDir + "쇼유.png", liquid: true, labelX: -190f),
        new SlotDef(IngredientType.TonkotsuBase, "돈코츠",   TopLeft, 330f, -550f, BinSize,
                    EtcDir + "돈코츠.png", liquid: true, labelX: -190f),

        // 좌하단: 육수 냄비. 화면에서 두 번째로 큰 물건.
        new SlotDef(IngredientType.Broth,        "육수",     MidLeft, 230f, -260f, PotSize,
                    EtcDir + "육수.png", liquid: true),

        // 하단: 면 2종과 조미료 2종을 한 줄로.
        // 두 튀김기 그림은 한 대를 반으로 자른 것이다. 얇은면은 오른쪽 끝이, 굵은면은 왼쪽 끝이
        // 잘려 있어서 "얇은면 → 굵은면" 순서로 딱 붙여 놓아야 한 대로 이어진다. 순서를 바꾸면 갈라진다.
        // 이어지려면 간격이 통 너비(256)와 정확히 같아야 한다. 벌어지거나 겹치면 이음매가 보인다.
        //
        // 면은 굵기가 달라도 IngredientType이 Noodles 하나뿐이라 채점에는 차이가 없다.
        // 그릇에 얹히는 그림만 갈린다. B가 enum을 나눠 주면 그때 종류를 구분한다.
        // Bowl의 면 1회 제한이 걸려 있어 둘 중 하나만 들어간다.
        new SlotDef(IngredientType.Noodles,      "얇은면",   Center, -330f, -380f, NoodleBinSize,
                    EtcDir + "얇은면.png", EtcDir + "얇은면 그릇용.png", idSuffix: "_Thin"),
        new SlotDef(IngredientType.Noodles,      "굵은면",   Center, -74f, -380f, NoodleBinSize,
                    EtcDir + "굵은면.png", EtcDir + "굵은면 그릇용.png", idSuffix: "_Thick"),

        // 조미료 2종. 클릭하면 젓가락 대신 병 자체를 들고, 그릇에 대면 기울여 뿌린다.
        // 그릇용 그림이 없어 수량만 세고 그릇에는 안 나온다.
        new SlotDef(IngredientType.FlavorOil,    "향미유",   Center, 175f, -380f, BinSize,
                    EtcDir + "향미유.png", liquid: true),
        new SlotDef(IngredientType.ChiliPowder,  "시치미",   Center, 365f, -380f, BinSize,
                    EtcDir + "시치미.png", liquid: true),

        // 우측: 재료통 7개를 세로 한 줄로. 이름표는 통 왼쪽에 붙여 줄 간격을 아낀다.
        new SlotDef(IngredientType.Chashu,       "차슈",     MidRight, -110f,  226f, BinSize,
                    IngredientDir + "차슈 재료통.png", IngredientDir + "차슈.png", IngredientDir + "차슈 테두리 강조.png",
                    labelX: -190f),
        new SlotDef(IngredientType.Menma,        "멘마",     MidRight, -110f,  364f, BinSize,
                    IngredientDir + "멘마 재료통.png", IngredientDir + "멘마.png", IngredientDir + "멘마 테두리 강조.png",
                    labelX: -190f),
        new SlotDef(IngredientType.GreenOnion,   "파",       MidRight, -110f,   88f, BinSize,
                    IngredientDir + "파 재료통.png", IngredientDir + "파.png", IngredientDir + "파 테두리 강조.png",
                    labelX: -190f),
        new SlotDef(IngredientType.Egg,          "계란",     MidRight, -110f,  -188f, BinSize,
                    IngredientDir + "계란 재료통.png", IngredientDir + "계란.png", IngredientDir + "계란 테두리 강조.png",
                    labelX: -190f),
        new SlotDef(IngredientType.Nori,         "김",       MidRight, -110f, -50f, BinSize,
                    IngredientDir + "김 재료통.png", IngredientDir + "김.png", IngredientDir + "김 테두리 강조.png",
                    labelX: -190f),
        // 숙주만 파일 이름이 "테두리 강조"가 아니라 "테두리"다.
        new SlotDef(IngredientType.BeanSprout,   "숙주",     MidRight, -110f, -464f, BinSize,
                    IngredientDir + "숙주 재료통.png", IngredientDir + "숙주.png", IngredientDir + "숙주 테두리.png",
                    labelX: -190f),
        new SlotDef(IngredientType.WoodEar,      "목이버섯", MidRight, -110f, -326f, BinSize,
                    IngredientDir + "목이버섯 재료통.png", IngredientDir + "목이버섯.png", IngredientDir + "목이버섯 테두리 강조.png",
                    labelX: -190f),
    };

    [MenuItem("Tools/Ramen/Build Cooking Layout")]
    public static void Build()
    {
        Font font = LoadFont();

        // 기존 것을 지우고 새로 만든다. 반복 실행해도 결과가 같아야 하므로.
        GameObject existing = GameObject.Find(CanvasName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        Transform canvas = CreateCanvas();
        EnsureEventSystem();
        EnsureGameManager();

        // 순서가 곧 그리기 순서다. 배경이 맨 처음, DragLayer가 맨 마지막.
        BuildBackground(canvas);
        TopBarRefs topBar = BuildTopBar(canvas, font);
        BuildSlots(CreateGroup("Slots", canvas), font);
        Bowl bowl = BuildBowl(canvas);

        // 폐기 버튼은 그릇보다 먼저 만들어지므로 둘이 다 생긴 뒤에 연결한다.
        WireDiscardButton(topBar.Discard, bowl);

        // 정산 팝업. 커서보다 아래여야 하므로 DragLayer보다 먼저 만든다.
        ResultPopupRefs popup = BuildResultPopup(canvas);
        FinalPopupRefs finalPopup = BuildFinalPopup(canvas);

        // 주문 화면. 조리 화면을 통째로 덮으므로 팝업들보다 뒤에 만든다.
        OrderScreenRefs orderScreen = BuildOrderScreen(canvas);
        RecipeBookRefs recipeBook = BuildRecipeBook(canvas);
        OrderResultRefs orderResult = BuildOrderResult(canvas);

        // 주문을 만들어 줄 B의 컴포넌트들을 씬에 올리고 GameManager와 잇는다.
        OrderSystemRefs orderSystem = EnsureOrderSystem(popup, finalPopup, orderScreen, recipeBook, orderResult);
        WireGameManager(orderSystem, topBar);

        // DragLayer는 반드시 마지막. 그래야 드래그 고스트와 커서가 항상 모든 UI 위에 그려진다.
        // 이 그룹 자체에는 Image를 붙이지 않는다. 붙이면 화면 전체를 덮어 모든 클릭을 삼킨다.
        BuildCursor(CreateGroup("DragLayer", canvas));

        // 더티 표시만 하면 디스크 파일은 그대로라, 이 상태로 커밋하면 옛 씬이 올라간다.
        // 실제로 한 번 그렇게 커밋돼서 클론 시 주문 시스템이 없는 씬이 나갔다. 그래서 바로 저장한다.
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = canvas.gameObject;
        Debug.Log("[RamenLayoutBuilder] " + SceneManager.GetActiveScene().name +
                  " 씬에 조리 UI를 생성했습니다. 슬롯 " + Slots.Length + "개.");
    }

    // ── 큰 덩어리 ────────────────────────────────────────────────

    private static Transform CreateCanvas()
    {
        var go = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = RefResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;   // 가로·세로 변화를 반반씩 반영

        return go.transform;
    }

    /// <summary>와이어프레임의 베이지 판. 이게 없으면 요소들이 허공에 뜬 것처럼 보인다.</summary>
    private static void BuildBackground(Transform canvas)
    {
        var go = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = Hex("#F5E9D0");
        img.raycastTarget = false;   // 배경이 클릭을 먹지 않도록
    }

    /// <summary>나중에 연결해야 하는 것들을 묶어 돌려준다.</summary>
    private static TopBarRefs BuildTopBar(Transform canvas, Font font)
    {
        Transform bar = CreateGroup("TopBar", canvas);
        Sprite panel = PanelSprite();

        // 주문 확인 ? 버튼 (자리만)
        Image help = CreateImage("OrderCheckButton", bar, TopLeft, new Vector2(70f, -55f), new Vector2(60f, 60f), Hex("#FFFFFF"), panel);
        CreateLabel(help.transform, "?", 35, InkColor, font);

        // 날짜 (자리만)
        Image day = CreateImage("DayPanel", bar, TopLeft, new Vector2(215f, -55f), new Vector2(190f, 60f), Hex("#FFFFFF"), panel);
        Text dayText = CreateLabel(day.transform, "1일차", 31, InkColor, font);

        // 제출 영역. 와이어프레임의 회색 가로 바.
        Image submit = CreateImage("SubmitZone", bar, TopCenter, new Vector2(0f, -55f), new Vector2(560f, 80f), Hex("#C9C9C9"), panel);
        Undo.AddComponent<SubmitZone>(submit.gameObject);
        CreateLabel(submit.transform, "제출하기", 33, InkColor, font);

        // 누적 매출. 재료비와 자본은 기획 확정으로 제거되어 누적 매출만 표시한다.
        Image revenue = CreateImage("RevenuePanel", bar, TopRight, new Vector2(-320f, -55f), new Vector2(380f, 60f), Hex("#FFFFFF"), panel);
        Text revenueText = CreateLabel(revenue.transform, "누적 수익 : 0₩", 27, InkColor, font);

        // 폐기 버튼. onClick은 그릇이 생긴 뒤 WireDiscardButton에서 붙인다.
        Image discard = CreateImage("DiscardButton", bar, TopRight, new Vector2(-60f, -55f), new Vector2(90f, 70f), Hex("#7BB661"), panel);
        CreateLabel(discard.transform, "폐기", 29, InkColor, font);

        // 손님 대사 줄. 주문 화면(B)이 아직 없어서 조리 화면 위에 글자로만 띄운다.
        return new TopBarRefs { Discard = discard, Help = help, DayText = dayText, RevenueText = revenueText };
    }

    /// <summary>
    /// 슬롯 하나 = 통 그림 + 아래에 이름.
    /// 고체는 IngredientSlot(드래그), 액체는 LiquidSlot(클릭해서 국자에 담기)이 붙는다.
    /// </summary>
    /// <summary>
    /// 하루 마감 때 뜨는 정산 팝업. 내용 갱신과 열고 닫기는 B의 DailyResultUI가 한다.
    /// 여기서는 그 스크립트가 요구하는 오브젝트만 만들어 준다.
    ///
    /// DailyResultUI가 TextMeshProUGUI를 요구하므로 이 팝업만 TMP를 쓴다.
    /// 조리 화면은 그대로 legacy Text다.
    /// </summary>
    private static ResultPopupRefs BuildResultPopup(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("ResultPopup", canvas);

        // 뒷판. raycastTarget을 켜 두어야 팝업이 떠 있는 동안 아래 조리 UI가 눌리지 않는다.
        var backdrop = CreateImage("Backdrop", root, Center, Vector2.zero, RefResolution, new Color(0f, 0f, 0f, 0.6f));
        backdrop.raycastTarget = true;

        Image panel = CreateImage("Panel", root, Center, Vector2.zero, new Vector2(760f, 460f),
                                  Hex("#FFF8E7"), PanelSprite());

        var title = CreateTmpText("TitleText", panel.transform, Center, new Vector2(0f, 150f),
                                  new Vector2(700f, 80f), "Day 1 정산", 52f, tmpFont);
        var profit = CreateTmpText("ProfitText", panel.transform, Center, new Vector2(0f, 40f),
                                   new Vector2(700f, 60f), "당일 총 수익 : 0원", 38f, tmpFont);
        var accuracy = CreateTmpText("AverageAccuracyText", panel.transform, Center, new Vector2(0f, -30f),
                                     new Vector2(700f, 60f), "평균 정확도 : 0.0%", 38f, tmpFont);

        Image confirmImage = CreateImage("ConfirmButton", panel.transform, Center, new Vector2(0f, -150f),
                                         new Vector2(260f, 80f), Hex("#7BB661"), PanelSprite());
        var confirm = Undo.AddComponent<Button>(confirmImage.gameObject);
        confirm.targetGraphic = confirmImage;
        CreateTmpText("Label", confirmImage.transform, Center, Vector2.zero,
                      new Vector2(240f, 60f), "확인", 36f, tmpFont);

        // 시작할 때는 닫혀 있어야 한다. DailyResultUI.Awake도 끄지만, 에디터에서도 가려지지 않게 여기서 끈다.
        root.gameObject.SetActive(false);

        return new ResultPopupRefs
        {
            Root = root.gameObject,
            Title = title,
            Profit = profit,
            Accuracy = accuracy,
            Confirm = confirm
        };
    }

    /// <summary>
    /// 손님 한 명분 결과창. 제출 직후에 뜨고 [확인]을 눌러야 다음 손님으로 넘어간다.
    /// </summary>
    private static OrderResultRefs BuildOrderResult(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("OrderResult", canvas);

        var backdrop = CreateImage("Backdrop", root, Center, Vector2.zero, RefResolution, new Color(0f, 0f, 0f, 0.7f));
        backdrop.raycastTarget = true;

        Image panel = CreateImage("Panel", root, Center, Vector2.zero, new Vector2(900f, 560f),
                                  Hex("#FFF8E7"), PanelSprite());

        var accuracy = CreateTmpText("AccuracyText", panel.transform, Center, new Vector2(0f, 190f),
                                     new Vector2(820f, 80f), "정확도 : 0%", 54f, tmpFont);

        // 이모지는 아이콘 아틀라스에서 잘라 쓴다. 표정은 OrderResultUI가 정확도로 고른다.
        Sprite[] faces = LoadEmojiSprites();
        Image emoji = CreateImage("Emoji", panel.transform, Center, new Vector2(0f, 60f),
                                  new Vector2(128f, 128f), Color.white,
                                  faces != null && faces.Length > 0 ? faces[0] : null);
        emoji.preserveAspect = true;

        var line = CreateTmpText("CustomerLine", panel.transform, Center, new Vector2(0f, -60f),
                                 new Vector2(820f, 60f), "잘 먹었습니다.", 38f, tmpFont);

        var reward = CreateTmpText("RewardText", panel.transform, Center, new Vector2(-200f, -150f),
                                   new Vector2(380f, 60f), "+ 0₩", 40f, tmpFont);
        var revenue = CreateTmpText("RevenueText", panel.transform, Center, new Vector2(200f, -150f),
                                    new Vector2(400f, 60f), "누적 수익 : 0₩", 32f, tmpFont);

        Image confirmImage = CreateImage("ConfirmButton", panel.transform, Center, new Vector2(0f, -230f),
                                         new Vector2(260f, 74f), Hex("#7BB661"), PanelSprite());
        var confirm = Undo.AddComponent<Button>(confirmImage.gameObject);
        confirm.targetGraphic = confirmImage;
        var confirmLabel = CreateTmpText("Label", confirmImage.transform, Center, Vector2.zero,
                                         new Vector2(240f, 60f), "확인", 34f, tmpFont);
        confirmLabel.color = Color.white;

        root.gameObject.SetActive(false);

        return new OrderResultRefs
        {
            Root = root.gameObject,
            Accuracy = accuracy,
            Reward = reward,
            Revenue = revenue,
            CustomerLine = line,
            Emoji = emoji,
            Faces = faces,
            Confirm = confirm
        };
    }

    /// <summary>
    /// Icon.png에서 표정 3종을 잘라 온다. 웃음 · 무표정 · 화남 순.
    /// 아틀라스가 균일 격자가 아니라(줄 간격이 17px) 필요한 칸만 직접 지정한다.
    /// </summary>
    private static Sprite[] LoadEmojiSprites()
    {
        const string path = "Assets/Art/UI/Icon.png";

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (importer == null || texture == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 아이콘 아틀라스를 찾지 못했습니다: " + path);
            return new Sprite[0];
        }

        // 그림 좌표는 위에서 아래, 유니티 텍스처 좌표는 아래에서 위라 y를 뒤집는다.
        const int cell = 16;
        const int topDownY = 34;   // 표정 줄의 위쪽 y. 아틀라스 셋째 줄이다.
        const int pitchX = 17;     // 칸 사이에 1px 간격이 있어 16이 아니라 17이다.
        int y = texture.height - topDownY - cell;

        // 칸 좌표까지 비교해야 한다. 개수만 보면 좌표를 고쳐도 다시 자르지 않는다.
        Rect expected = new Rect(0f, y, cell, cell);
        bool needsSlice = importer.spriteImportMode != SpriteImportMode.Multiple
                          || importer.spritesheet == null
                          || importer.spritesheet.Length != 3
                          || importer.spritesheet[0].rect != expected;

        if (needsSlice
            || importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var slices = new SpriteMetaData[3];
            for (int i = 0; i < 3; i++)
            {
                slices[i] = new SpriteMetaData
                {
                    name = "Face_" + i,
                    rect = new Rect(i * pitchX, y, cell, cell),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };
            }

            importer.spritesheet = slices;
            importer.SaveAndReimport();
        }

        var faces = new Sprite[3];
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var sprite = asset as Sprite;
            if (sprite == null) continue;

            int index;
            int underscore = sprite.name.LastIndexOf('_');
            if (underscore < 0 || !int.TryParse(sprite.name.Substring(underscore + 1), out index)) continue;
            if (index >= 0 && index < 3) faces[index] = sprite;
        }

        return faces;
    }

    /// <summary>
    /// ? 버튼이 여는 정보 패널. 주문 원문·기본 레시피·재료 속성표를 한 화면에 놓는다.
    /// 표는 이름 열과 내용 열을 따로 둔다. 한글은 글자 폭이 제각각이라 한 덩이 텍스트로는 줄이 안 맞는다.
    /// </summary>
    private static RecipeBookRefs BuildRecipeBook(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("RecipeBook", canvas);

        var backdrop = CreateImage("Backdrop", root, Center, Vector2.zero, RefResolution, new Color(0f, 0f, 0f, 0.75f));
        backdrop.raycastTarget = true;

        Image panel = CreateImage("Panel", root, Center, Vector2.zero, new Vector2(1660f, 900f),
                                  Hex("#FFF8E7"), PanelSprite());

        CreateTmpText("Title", panel.transform, Center, new Vector2(0f, 390f),
                      new Vector2(800f, 70f), "주문 확인", 46f, tmpFont);

        // 왼쪽: 손님 주문 원문
        CreateTmpText("OrderHeader", panel.transform, Center, new Vector2(-540f, 300f),
                      new Vector2(480f, 50f), "손님 주문", 34f, tmpFont);
        var order = CreateTmpText("OrderText", panel.transform, Center, new Vector2(-540f, 70f),
                                  new Vector2(480f, 420f), "", 28f, tmpFont);
        order.alignment = TextAlignmentOptions.TopLeft;

        // 오른쪽 위: 기본 레시피
        CreateTmpText("RecipeHeader", panel.transform, Center, new Vector2(120f, 300f),
                      new Vector2(700f, 50f), "기본 레시피", 34f, tmpFont);
        var recipeNames = CreateTmpText("RecipeNames", panel.transform, Center, new Vector2(-90f, 160f),
                                        new Vector2(160f, 200f), "", 24f, tmpFont);
        recipeNames.alignment = TextAlignmentOptions.TopRight;
        var recipeValues = CreateTmpText("RecipeValues", panel.transform, Center, new Vector2(405f, 160f),
                                         new Vector2(790f, 200f), "", 24f, tmpFont);
        recipeValues.alignment = TextAlignmentOptions.TopLeft;

        // 줄바꿈이 생기면 왼쪽 이름 열과 줄이 어긋난다. 한 메뉴는 반드시 한 줄이어야 한다.
        recipeValues.textWrappingMode = TextWrappingModes.NoWrap;

        // 오른쪽 아래: 재료 속성표
        CreateTmpText("IngredientHeader", panel.transform, Center, new Vector2(120f, 60f),
                      new Vector2(700f, 50f), "재료 속성", 34f, tmpFont);
        var ingNames = CreateTmpText("IngredientNames", panel.transform, Center, new Vector2(-90f, -170f),
                                     new Vector2(160f, 380f), "", 24f, tmpFont);
        ingNames.alignment = TextAlignmentOptions.TopRight;
        var ingAttrs = CreateTmpText("IngredientAttrs", panel.transform, Center, new Vector2(405f, -170f),
                                     new Vector2(790f, 380f), "", 24f, tmpFont);
        ingAttrs.alignment = TextAlignmentOptions.TopLeft;
        ingAttrs.textWrappingMode = TextWrappingModes.NoWrap;

        Image closeImage = CreateImage("CloseButton", panel.transform, Center, new Vector2(0f, -390f),
                                       new Vector2(260f, 76f), Hex("#7BB661"), PanelSprite());
        var close = Undo.AddComponent<Button>(closeImage.gameObject);
        close.targetGraphic = closeImage;
        var closeLabel = CreateTmpText("Label", closeImage.transform, Center, Vector2.zero,
                                       new Vector2(240f, 60f), "닫기", 34f, tmpFont);
        closeLabel.color = Color.white;

        root.gameObject.SetActive(false);

        return new RecipeBookRefs
        {
            Root = root.gameObject,
            Order = order,
            RecipeNames = recipeNames,
            RecipeValues = recipeValues,
            IngredientNames = ingNames,
            IngredientAttrs = ingAttrs,
            Close = close
        };
    }

    /// <summary>
    /// 손님에게 주문을 받는 화면. 와이어프레임 기준으로 밤 포장마차 한 칸이다.
    /// 손님 그림이 아직 없어 자리만 회색 판으로 잡아 둔다.
    /// </summary>
    private static OrderScreenRefs BuildOrderScreen(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("OrderScreen", canvas);

        // 밤 배경. 불투명이라 조리 화면을 완전히 가리고, 레이캐스트도 여기서 막힌다.
        var night = CreateImage("Night", root, Center, Vector2.zero, RefResolution, DarkHex("#14100E"));
        night.raycastTarget = true;

        // 카운터 뒤쪽 벽. 배경만 단색이면 깊이가 안 보여서 한 단 넣는다.
        CreateImage("BackWall", root, Center, new Vector2(0f, 200f), new Vector2(1920f, 680f), DarkHex("#241A15"));

        // 카운터. 앞면 띠와 상판으로 나뉜다.
        CreateImage("CounterFront", root, Center, new Vector2(0f, -180f), new Vector2(1920f, 140f), DarkHex("#5C3A21"));
        CreateImage("CounterTop", root, Center, new Vector2(0f, -395f), new Vector2(1920f, 290f), Hex("#F5E9D0"));

        // 손님 자리. 아트가 오면 이 Image의 스프라이트만 갈아 끼우면 된다.
        CreateImage("CustomerSlot", root, Center, new Vector2(0f, 105f), new Vector2(380f, 570f), Hex("#E8B98F"));

        // 말풍선 꼬리. 정사각형을 45도 돌려 절반을 말풍선 뒤에 숨기면 삼각형으로 보인다.
        // 반드시 말풍선보다 먼저 만들어야 한다. 뒤에 만들면 사각형 그대로 드러난다.
        Image tail = CreateImage("BubbleTail", root, Center, new Vector2(-150f, 60f),
                                 new Vector2(90f, 90f), Hex("#FFF8E7"));
        tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tail.raycastTarget = false;

        Image bubble = CreateImage("Bubble", root, Center, new Vector2(-540f, 150f), new Vector2(740f, 560f),
                                   Hex("#FFF8E7"), PanelSprite());

        // 대사는 페르소나·난이도에 따라 6~8줄까지 간다. 글자 크기를 자동으로 줄여
        // 상자를 넘지 않게 하고, 버튼 자리는 따로 비워 둔다.
        var dialogue = CreateTmpText("DialogueText", bubble.transform, Center, new Vector2(0f, 60f),
                                     new Vector2(660f, 340f), "손님을 기다리는 중...", 30f, tmpFont);
        dialogue.alignment = TextAlignmentOptions.TopLeft;
        dialogue.enableAutoSizing = true;
        dialogue.fontSizeMin = 14f;
        dialogue.fontSizeMax = 30f;

        Image startImage = CreateImage("StartButton", bubble.transform, Center, new Vector2(180f, -210f),
                                       new Vector2(300f, 80f), Hex("#C0392B"), PanelSprite());
        var start = Undo.AddComponent<Button>(startImage.gameObject);
        start.targetGraphic = startImage;
        var startLabel = CreateTmpText("Label", startImage.transform, Center, Vector2.zero,
                                       new Vector2(280f, 60f), "조리 시작  →", 34f, tmpFont);
        startLabel.color = Color.white;

        Image dayPanel = CreateImage("DayTimePanel", root, TopLeft, new Vector2(360f, -60f),
                                     new Vector2(600f, 72f), Hex("#FFF8E7"), PanelSprite());
        var dayTime = CreateTmpText("DayTimeText", dayPanel.transform, Center, Vector2.zero,
                                    new Vector2(570f, 60f), "영업 시간 1일차 / 19 : 00", 34f, tmpFont);

        Image revenuePanel = CreateImage("RevenuePanel", root, TopRight, new Vector2(-300f, -60f),
                                         new Vector2(480f, 72f), Hex("#FFF8E7"), PanelSprite());
        var revenue = CreateTmpText("RevenueText", revenuePanel.transform, Center, Vector2.zero,
                                    new Vector2(450f, 60f), "누적 수익 : 0₩", 34f, tmpFont);

        root.gameObject.SetActive(false);

        return new OrderScreenRefs
        {
            Root = root.gameObject,
            DayTime = dayTime,
            Revenue = revenue,
            Dialogue = dialogue,
            Start = start
        };
    }

    /// <summary>5일 영업이 끝났을 때 뜨는 최종 성적표. 정산 팝업과 같은 짜임새다.</summary>
    private static FinalPopupRefs BuildFinalPopup(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("FinalResultPopup", canvas);

        var backdrop = CreateImage("Backdrop", root, Center, Vector2.zero, RefResolution, new Color(0f, 0f, 0f, 0.7f));
        backdrop.raycastTarget = true;

        Image panel = CreateImage("Panel", root, Center, Vector2.zero, new Vector2(820f, 560f),
                                  Hex("#FFF8E7"), PanelSprite());

        var title = CreateTmpText("TitleText", panel.transform, Center, new Vector2(0f, 195f),
                                  new Vector2(760f, 80f), "5일 영업 종료", 56f, tmpFont);
        var revenue = CreateTmpText("RevenueText", panel.transform, Center, new Vector2(0f, 80f),
                                    new Vector2(760f, 60f), "누적 매출 : 0원", 40f, tmpFont);
        var accuracy = CreateTmpText("AccuracyText", panel.transform, Center, new Vector2(0f, 10f),
                                     new Vector2(760f, 60f), "평균 정확도 : 0.0%", 40f, tmpFont);
        var perfect = CreateTmpText("PerfectText", panel.transform, Center, new Vector2(0f, -60f),
                                    new Vector2(760f, 60f), "완벽한 한 그릇 : 0 / 0건", 40f, tmpFont);

        Image restartImage = CreateImage("RestartButton", panel.transform, Center, new Vector2(0f, -190f),
                                         new Vector2(300f, 84f), Hex("#7BB661"), PanelSprite());
        var restart = Undo.AddComponent<Button>(restartImage.gameObject);
        restart.targetGraphic = restartImage;
        CreateTmpText("Label", restartImage.transform, Center, Vector2.zero,
                      new Vector2(280f, 60f), "다시 시작", 38f, tmpFont);

        root.gameObject.SetActive(false);

        return new FinalPopupRefs
        {
            Root = root.gameObject,
            Title = title,
            Revenue = revenue,
            Accuracy = accuracy,
            Perfect = perfect,
            Restart = restart
        };
    }

    /// <summary>
    /// 조리 화면과 같은 글꼴의 TMP 폰트 애셋. 없으면 만들어서 프로젝트에 저장한다.
    /// 한글은 글자 수가 많아 정적 아틀라스로 구우면 용량이 커지므로 Dynamic으로 둔다.
    /// Dynamic은 실제로 쓰인 글자만 실행 중에 아틀라스로 채운다.
    /// </summary>
    private static TMP_FontAsset EnsureTmpFont()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
        if (existing != null) return existing;

        Font source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (source == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] " + FontPath + " 을(를) 찾지 못해 TMP 폰트를 만들지 못했습니다.");
            return null;
        }

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
            source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);

        if (asset == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] TMP 폰트 애셋 생성에 실패했습니다.");
            return null;
        }

        asset.name = "ThinMulmaru Mono SDF";
        AssetDatabase.CreateAsset(asset, TmpFontPath);

        // 아틀라스 텍스처와 머티리얼을 같은 파일 안에 넣어야 참조가 끊기지 않는다.
        if (asset.atlasTextures != null && asset.atlasTextures.Length > 0)
        {
            asset.atlasTextures[0].name = "ThinMulmaru Mono Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        }
        if (asset.material != null)
        {
            asset.material.name = "ThinMulmaru Mono SDF Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[RamenLayoutBuilder] TMP 폰트 애셋을 만들었습니다: " + TmpFontPath);
        return asset;
    }

    private static TextMeshProUGUI CreateTmpText(string name, Transform parent, Vector2 anchor, Vector2 pos,
                                                 Vector2 size, string content, float fontSize, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = PopupInkColor;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void BuildSlots(Transform parent, Font font)
    {
        foreach (SlotDef def in Slots)
        {
            Image bin = CreateImage("Slot_" + def.Type + (def.IdSuffix ?? ""), parent, def.Anchor, def.Pos, def.Size,
                                    Color.white, LoadSprite(def.BinPath));
            bin.preserveAspect = true;

            if (def.Liquid)
            {
                LiquidSlot slot = Undo.AddComponent<LiquidSlot>(bin.gameObject);
                slot.type = def.Type;
            }
            else
            {
                IngredientSlot slot = Undo.AddComponent<IngredientSlot>(bin.gameObject);
                slot.type = def.Type;
                slot.bowlSprite = LoadSprite(def.BowlPath);
                slot.dragSprite = LoadSprite(def.DragPath);

                // 통이 표준보다 크면 원본 그림도 그만큼 크다. 면 통 384는 2배, 나머지 192는 1배.
                slot.ghostScale = def.Size.x / BinSize.x;
            }

            // 커서가 젓가락 그림이라 어느 통을 가리키는지 알기 어렵다. 통이 직접 반응하게 한다.
            Undo.AddComponent<SlotHover>(bin.gameObject);

            CreateSlotLabel(bin.transform, def, font);
        }
    }

    /// <summary>시스템 커서를 대신하는 조리 도구. 평소 젓가락, 액체를 뜨면 국자.</summary>
    private static void BuildCursor(Transform dragLayer)
    {
        Sprite[] frames = LoadChopstickFrames();

        Image img = CreateImage("Cursor", dragLayer, Center, Vector2.zero, CursorSize,
                                Color.white, frames != null && frames.Length > 0 ? frames[0] : null);
        img.preserveAspect = true;

        // 커서가 레이캐스트를 먹으면 마우스 밑이 늘 커서라 아무것도 클릭할 수 없다.
        img.raycastTarget = false;

        var cursor = Undo.AddComponent<CookingCursor>(img.gameObject);
        cursor.chopstickFrames = frames;
        cursor.ladleEmpty = LoadSprite(EtcDir + "국자.png");
        cursor.ladleShio = LoadSprite(EtcDir + "국자 시오.png");
        cursor.ladleShoyu = LoadSprite(EtcDir + "국자 쇼유.png");
        cursor.ladleTonkotsu = LoadSprite(EtcDir + "국자 돈코츠.png");
        cursor.ladleBroth = LoadSprite(EtcDir + "국자 육수.png");
        cursor.flavorOilBottle = LoadSprite(EtcDir + "향미유.png");
        cursor.chiliPowderBottle = LoadSprite(EtcDir + "시치미.png");
    }

    /// <summary>
    /// 젓가락은 한 장에 64px 프레임 3개(벌림·중간·집음)가 가로로 붙어 있다.
    /// Multiple로 잘라 세 조각을 만든 뒤 왼쪽부터 순서대로 돌려준다.
    /// </summary>
    private static Sprite[] LoadChopstickFrames()
    {
        const int frameCount = 3;
        const float frameSize = 64f;
        string path = EtcDir + "젓가락.png";

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 젓가락 그림을 찾지 못했습니다: " + path);
            return null;
        }

        if (importer.spriteImportMode != SpriteImportMode.Multiple || importer.spritesheet.Length != frameCount)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var sheet = new SpriteMetaData[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                sheet[i].name = "Chopstick_" + i;
                sheet[i].rect = new Rect(i * frameSize, 0f, frameSize, frameSize);
                sheet[i].pivot = new Vector2(0.5f, 0.5f);
                sheet[i].alignment = (int)SpriteAlignment.Center;
            }
            importer.spritesheet = sheet;
            importer.SaveAndReimport();
        }

        var frames = new Sprite[frameCount];
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var sprite = asset as Sprite;
            if (sprite == null) continue;

            int index = Mathf.RoundToInt(sprite.rect.x / frameSize);
            if (index >= 0 && index < frameCount) frames[index] = sprite;
        }

        if (frames[0] == null) Debug.LogWarning("[RamenLayoutBuilder] 젓가락 프레임을 잘라내지 못했습니다.");
        return frames;
    }

    private static Bowl BuildBowl(Transform canvas)
    {
        Image bowl = CreateImage("Bowl", canvas, Center, new Vector2(0f, 90f), BowlSize,
                                 Color.white, LoadSprite(BowlDir + "빈그릇.png"));
        bowl.preserveAspect = true;

        Bowl component = Undo.AddComponent<Bowl>(bowl.gameObject);
        component.emptyBowlSprite = LoadSprite(BowlDir + "빈그릇.png");
        component.brothBowlSprite = LoadSprite(BowlDir + "육수그릇.png");
        component.shioBowlSprite = LoadSprite(BowlDir + "시오그릇.png");
        component.shoyuBowlSprite = LoadSprite(BowlDir + "쇼유그릇.png");
        component.tonkotsuBowlSprite = LoadSprite(BowlDir + "돈코츠그릇.png");

        // 국물이 차오르는 8프레임(128px 4열 x 2행). 재생 속도는 Bowl.brothPourFps로 조절한다.
        component.brothPourFrames = LoadSpriteSheet(BowlDir + "애니메이션_육수그릇.png", 128, 128);

        // 드래그 중에 레이캐스트를 통과시키려면 CanvasGroup이 필요하다.
        // 없으면 그릇 자신이 SubmitZone을 가려서 제출이 영영 안 된다.
        Undo.AddComponent<CanvasGroup>(bowl.gameObject);

        // 그릇 안 재료 그림이 들어갈 자리. Bowl이 런타임에 여기로 넣는다.
        CreateGroup("Contents", bowl.transform);

        return component;
    }

    /// <summary>폐기 버튼 클릭을 Bowl.Discard에 붙인다. 인스펙터에 남는 연결이라 씬을 저장하면 유지된다.</summary>
    private static void WireDiscardButton(Image discard, Bowl bowl)
    {
        var button = Undo.AddComponent<Button>(discard.gameObject);
        button.targetGraphic = discard;
        UnityEventTools.AddPersistentListener(button.onClick, bowl.Discard);
    }

    // ── 씬에 하나만 있어야 하는 것들 ───────────────────────────────

    private static void EnsureEventSystem()
    {
        EventSystem es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            es = go.GetComponent<EventSystem>();
        }

        // 이 프로젝트는 Input System 전용(activeInputHandler=1)이라 구 모듈은 이벤트를 하나도 못 보낸다.
        StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null)
        {
            Undo.DestroyObjectImmediate(legacy);
        }

        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            Undo.AddComponent<InputSystemUIInputModule>(es.gameObject);
        }
    }

    /// <summary>
    /// 주문을 만들어 주는 B의 컴포넌트들을 한 오브젝트에 올리고 서로 물려 준다.
    /// 전부 MonoBehaviour라 씬에 없으면 주문이 아예 생성되지 않는다.
    /// 이미 있으면 인스펙터에서 손댄 값이 날아가지 않도록 그대로 둔다.
    /// </summary>
    private static OrderSystemRefs EnsureOrderSystem(ResultPopupRefs popup, FinalPopupRefs finalPopup,
                                                     OrderScreenRefs orderScreen, RecipeBookRefs recipeBook,
                                                     OrderResultRefs orderResult)
    {
        // 예전 빌드로 만든 OrderSystem에는 DayManager가 없다. 남겨 두고 컴포넌트만 덧붙이면
        // 배선이 반쯤 빈 채로 남을 수 있어서, 캔버스와 같은 방식으로 지우고 새로 만든다.
        OrderManager existing = Object.FindFirstObjectByType<OrderManager>();
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var go = new GameObject("OrderSystem",
                                typeof(DialogueScenarioGenerator),
                                typeof(RamenCalculator),
                                typeof(DayManager),
                                typeof(DailyResultUI),
                                typeof(FinalResultUI),
                                typeof(OrderScreenUI),
                                typeof(RecipeBookUI),
                                typeof(OrderResultUI),
                                typeof(OrderManager));
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);

        var scenarioGenerator = go.GetComponent<DialogueScenarioGenerator>();
        var calculator = go.GetComponent<RamenCalculator>();
        var dayManager = go.GetComponent<DayManager>();
        var manager = go.GetComponent<OrderManager>();

        // 전부 private [SerializeField]라 직접 대입할 수 없다.
        // 대사·주문 생성은 DialogueMacroSystem이 통째로 맡는다. 표 데이터는
        // Assets/Resources/DialogueDB.json 에서 생성기가 알아서 읽으므로 따로 꽂을 게 없다.
        SetPrivateReference(manager, "dialogueScenarioGenerator", scenarioGenerator);
        SetPrivateReference(manager, "ramenCalculator", calculator);

        // OrderManager는 일차를 DayManager에서만 읽는다. 안 꽂으면 항상 1일차로 주문이 생긴다.
        SetPrivateReference(manager, "dayManager", dayManager);

        SetPrivateReference(dayManager, "orderManager", manager);
        SetPrivateReference(dayManager, "ramenCalculator", calculator);

        // 정산 팝업. DailyResultUI는 Awake에서 popupRoot를 꺼 버리므로
        // 팝업 자신이 아니라 항상 살아 있는 이 오브젝트에 붙여야 한다.
        var resultUI = go.GetComponent<DailyResultUI>();
        SetPrivateReference(dayManager, "dailyResultUI", resultUI);
        SetPrivateReference(resultUI, "dayManager", dayManager);

        if (popup != null)
        {
            SetPrivateReference(resultUI, "popupRoot", popup.Root);
            SetPrivateReference(resultUI, "titleText", popup.Title);
            SetPrivateReference(resultUI, "profitText", popup.Profit);
            SetPrivateReference(resultUI, "averageAccuracyText", popup.Accuracy);
            SetPrivateReference(resultUI, "confirmButton", popup.Confirm);
        }

        // 5일 완료 화면. 이것도 popupRoot를 끄는 쪽이라 팝업 바깥에 붙여야 한다.
        var finalUI = go.GetComponent<FinalResultUI>();
        if (finalPopup != null)
        {
            SetPrivateReference(finalUI, "popupRoot", finalPopup.Root);
            SetPrivateReference(finalUI, "titleText", finalPopup.Title);
            SetPrivateReference(finalUI, "revenueText", finalPopup.Revenue);
            SetPrivateReference(finalUI, "accuracyText", finalPopup.Accuracy);
            SetPrivateReference(finalUI, "perfectText", finalPopup.Perfect);
            SetPrivateReference(finalUI, "restartButton", finalPopup.Restart);
        }

        // 주문 화면. 이것도 screenRoot를 끄는 쪽이라 화면 바깥에 붙여야 한다.
        var orderScreenUI = go.GetComponent<OrderScreenUI>();
        if (orderScreen != null)
        {
            SetPrivateReference(orderScreenUI, "screenRoot", orderScreen.Root);
            SetPrivateReference(orderScreenUI, "dayTimeText", orderScreen.DayTime);
            SetPrivateReference(orderScreenUI, "revenueText", orderScreen.Revenue);
            SetPrivateReference(orderScreenUI, "dialogueText", orderScreen.Dialogue);
            SetPrivateReference(orderScreenUI, "startButton", orderScreen.Start);
        }

        // 정보 패널(? 버튼). 이것도 root를 끄는 쪽이라 패널 바깥에 붙여야 한다.
        var book = go.GetComponent<RecipeBookUI>();
        if (recipeBook != null)
        {
            SetPrivateReference(book, "root", recipeBook.Root);
            SetPrivateReference(book, "orderText", recipeBook.Order);
            SetPrivateReference(book, "recipeNames", recipeBook.RecipeNames);
            SetPrivateReference(book, "recipeValues", recipeBook.RecipeValues);
            SetPrivateReference(book, "ingredientNames", recipeBook.IngredientNames);
            SetPrivateReference(book, "ingredientAttrs", recipeBook.IngredientAttrs);
            SetPrivateReference(book, "closeButton", recipeBook.Close);
        }

        // 손님별 결과창.
        var result = go.GetComponent<OrderResultUI>();
        if (orderResult != null)
        {
            SetPrivateReference(result, "root", orderResult.Root);
            SetPrivateReference(result, "accuracyText", orderResult.Accuracy);
            SetPrivateReference(result, "rewardText", orderResult.Reward);
            SetPrivateReference(result, "revenueText", orderResult.Revenue);
            SetPrivateReference(result, "customerLine", orderResult.CustomerLine);
            SetPrivateReference(result, "emoji", orderResult.Emoji);
            SetPrivateReference(result, "confirmButton", orderResult.Confirm);
            SetPrivateReference(result, "gameManager", Object.FindFirstObjectByType<GameManager>());

            if (orderResult.Faces != null && orderResult.Faces.Length >= 3)
            {
                SetPrivateReference(result, "emojiHappy", orderResult.Faces[0]);
                SetPrivateReference(result, "emojiNeutral", orderResult.Faces[1]);
                SetPrivateReference(result, "emojiAngry", orderResult.Faces[2]);
            }
        }

        return new OrderSystemRefs { Order = manager, Day = dayManager };
    }

    /// <summary>GameManager가 주문·표시할 글자를 찾을 수 있도록 꽂아 준다.</summary>
    private static void WireGameManager(OrderSystemRefs orderSystem, TopBarRefs topBar)
    {
        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        if (gameManager == null) return;

        SetPrivateReference(gameManager, "orderManager", orderSystem.Order);
        SetPrivateReference(gameManager, "dayManager", orderSystem.Day);
        SetPrivateReference(gameManager, "revenueText", topBar.RevenueText);
        SetPrivateReference(gameManager, "dayText", topBar.DayText);
        SetPrivateReference(gameManager, "ramenCalculator", Object.FindFirstObjectByType<RamenCalculator>());
        SetPrivateReference(gameManager, "finalResultUI", Object.FindFirstObjectByType<FinalResultUI>());
        SetPrivateReference(gameManager, "orderScreenUI", Object.FindFirstObjectByType<OrderScreenUI>());
        SetPrivateReference(gameManager, "recipeBookUI", Object.FindFirstObjectByType<RecipeBookUI>());
        SetPrivateReference(gameManager, "orderResultUI", Object.FindFirstObjectByType<OrderResultUI>());

        // ? 버튼: 주문 원문 + 기본 레시피 + 재료 속성표 (기획서 6.1).
        // 조리 화면에는 대사줄이 없으므로 이게 유일한 확인 수단이다.
        var help = Undo.AddComponent<Button>(topBar.Help.gameObject);
        help.targetGraphic = topBar.Help;
        UnityEventTools.AddPersistentListener(help.onClick, gameManager.ShowOrderInfo);
    }

    /// <summary>private [SerializeField] 칸에 값을 넣는다. 인스펙터로 꽂는 것과 같은 결과.</summary>
    private static void SetPrivateReference(Object target, string fieldName, Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] " + target.GetType().Name + "." + fieldName + " 을(를) 찾지 못했습니다.");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureGameManager()
    {
        // 이미 있으면 인스펙터 연결이 날아가지 않도록 그대로 둔다.
        if (Object.FindFirstObjectByType<GameManager>() != null) return;

        var go = new GameObject("GameManager", typeof(GameManager));
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
    }

    // ── 작은 도우미들 ─────────────────────────────────────────────

    /// <summary>자식들이 화면 가장자리를 기준으로 자리를 잡도록 부모를 화면 전체로 늘린다.</summary>
    private static Transform CreateGroup(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static Image CreateImage(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = color;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
        }
        return img;
    }

/// <summary>
    /// 통 그림에 붙는 이름표. 기본은 바로 아래고, LabelOffset을 주면 그 자리로 간다.
    /// 우측 재료통처럼 세로로 빽빽하게 세운 줄은 이름표를 옆에 붙여야 자리가 나온다.
    /// </summary>
    private static void CreateSlotLabel(Transform parent, SlotDef def, Font font)
    {
        bool beside = !Mathf.Approximately(def.LabelOffset.x, 0f);

        Vector2 pos = def.LabelOffset == Vector2.zero
            ? new Vector2(0f, -def.Size.y * 0.5f - 14f)
            : def.LabelOffset;

        Vector2 size = beside ? new Vector2(175f, 40f) : new Vector2(def.Size.x + 80f, 30f);
        TextAnchor align = beside ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;

        CreateText("Label", parent, Center, pos, size, def.Label, SlotLabelSize, InkColor, font, align);
    }

    private static Text CreateText(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size,
                                   string content, int fontSize, Color color, Font font, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        return ConfigureText(go.GetComponent<Text>(), content, fontSize, color, font, align);
    }

    /// <summary>부모를 꽉 채우는 라벨. 버튼 위에 글자를 얹을 때 쓴다.</summary>
    private static Text CreateLabel(Transform parent, string content, int fontSize, Color color, Font font)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return ConfigureText(go.GetComponent<Text>(), content, fontSize, color, font, TextAnchor.MiddleCenter);
    }

    private static Text ConfigureText(Text text, string content, int fontSize, Color color, Font font, TextAnchor align)
    {
        text.text = content;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        // 라벨이 레이캐스트를 먹으면 슬롯의 드래그가 시작되지 않는다.
        text.raycastTarget = false;

        // 흰 글자를 배경 위에서 읽히게 하는 검은 테두리.
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = TextOutlineColor;
        outline.effectDistance = TextOutlineDistance;

        return text;
    }

    /// <summary>내장 둥근 사각형. 9슬라이스라 어느 크기로 늘려도 모서리가 뭉개지지 않는다.</summary>
    private static Sprite PanelSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    /// <summary>
    /// 그림 하나를 불러온다. 이 프로젝트의 PNG 기본 임포트는 Multiple + 압축 + Bilinear라
    /// 그대로 두면 픽셀아트가 흐려지고, 그림을 갈아 끼울 때 씬의 참조가 끊긴다.
    /// 그래서 불러오기 전에 설정을 확인하고 어긋나 있으면 바로잡는다.
    /// </summary>
    /// <summary>
    /// 한 장에 여러 칸이 들어 있는 그림을 격자로 잘라 프레임 배열로 돌려준다.
    /// LoadSprite는 Single을 강제하므로 애니메이션 시트에는 쓸 수 없어 따로 둔다.
    /// 잘린 순서는 왼쪽 위에서 오른쪽으로, 그다음 아랫줄이다.
    /// </summary>
    private static Sprite[] LoadSpriteSheet(string path, int cellWidth, int cellHeight)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 그림을 찾지 못했습니다: " + path);
            return new Sprite[0];
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) return new Sprite[0];

        int cols = texture.width / cellWidth;
        int rows = texture.height / cellHeight;
        if (cols <= 0 || rows <= 0)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 칸 크기가 그림보다 큽니다: " + path);
            return new Sprite[0];
        }

        // 이미 같은 개수로 잘려 있으면 다시 임포트하지 않는다. 재임포트는 느리다.
        bool needsSlice = importer.spriteImportMode != SpriteImportMode.Multiple
                          || importer.spritesheet == null
                          || importer.spritesheet.Length != cols * rows;

        if (needsSlice
            || importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            string baseName = System.IO.Path.GetFileNameWithoutExtension(path);
            var slices = new System.Collections.Generic.List<SpriteMetaData>();

            // 유니티 텍스처 좌표는 아래가 0이라, 위에서부터 세려면 y를 뒤집어야 한다.
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    var meta = new SpriteMetaData();
                    meta.name = baseName + "_" + (row * cols + col);
                    meta.rect = new Rect(col * cellWidth,
                                         texture.height - (row + 1) * cellHeight,
                                         cellWidth, cellHeight);
                    meta.alignment = (int)SpriteAlignment.Center;
                    meta.pivot = new Vector2(0.5f, 0.5f);
                    slices.Add(meta);
                }
            }

            importer.spritesheet = slices.ToArray();
            importer.SaveAndReimport();
        }

        // LoadAllAssetsAtPath는 순서를 보장하지 않는다. 이름 끝 번호로 다시 세운다.
        var frames = new Sprite[cols * rows];
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var sprite = asset as Sprite;
            if (sprite == null) continue;

            int underscore = sprite.name.LastIndexOf('_');
            int index;
            if (underscore < 0 || !int.TryParse(sprite.name.Substring(underscore + 1), out index)) continue;
            if (index >= 0 && index < frames.Length) frames[index] = sprite;
        }

        int missing = 0;
        foreach (var f in frames) if (f == null) missing++;
        if (missing > 0)
        {
            Debug.LogWarning("[RamenLayoutBuilder] " + path + " 에서 프레임 " + missing + "개를 못 찾았습니다.");
        }

        return frames;
    }

    private static Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 그림을 찾지 못했습니다: " + path);
            return null;
        }

        if (importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;   // 픽셀아트라 보간하면 안 된다
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 스프라이트를 만들지 못했습니다: " + path);
        }
        return sprite;
    }

    private static Font LoadFont()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font != null) return font;

        Debug.LogWarning("[RamenLayoutBuilder] 폰트를 찾지 못했습니다: " + FontPath +
                         "\n내장 LegacyRuntime.ttf로 대체합니다. 한글이 네모로 보이면 맑은고딕을 이 경로에 넣어 주세요.");
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    /// <summary>
    /// 어두운 색 전용. 프로젝트가 Linear 색공간이라 Hex 값을 그대로 넘기면
    /// 유니티가 그것을 이미 리니어인 값으로 보고 sRGB로 되돌려 내보내 크게 밝아진다.
    /// (#0B0908의 0.043이 화면에서는 0.230이 된다.)
    /// 미리 linear로 바꿔 넘기면 지정한 색 그대로 보인다.
    /// 밝은 색은 차이가 눈에 안 띄어 기존 Hex를 그대로 쓴다.
    /// </summary>
    private static Color DarkHex(string hex)
    {
        return Hex(hex).linear;
    }

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
    }
}
