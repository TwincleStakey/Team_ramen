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
    private const string UndoLabel = "Build Cooking Layout";

    // ── 폰트 ─────────────────────────────────────────────────────────────────
    // 폰트는 언제든 되돌릴 수 있어야 한다(팀장 지시). 바꿀 곳은 ActiveFont 한 줄뿐이고,
    // 바꾼 뒤 Tools/Ramen/Build Cooking Layout을 다시 누르면 끝난다.
    //
    // TMP 에셋 경로를 프로필마다 다르게 둔 것이 핵심이다. 경로가 같으면 EnsureTmpFont가
    // 먼저 만들어 둔 옛 에셋을 그대로 돌려줘서, ttf만 바꿔도 화면 글자는 안 바뀐다.
    private enum FontChoice { Galmuri11, ThinMulmaru }

    private const FontChoice ActiveFont = FontChoice.Galmuri11;

    private class FontProfile
    {
        public string TtfPath;
        public string TmpAssetPath;
        public string TmpAssetName;

        /// <summary>
        /// 아틀라스를 구울 때 쓰는 크기. 화면 글자 크기는 이 값의 정수배만 쓴다.
        ///
        /// 갈무리 글자 자체는 11픽셀인데 11로 구우면 안 된다. FreeType가 11픽셀짜리 그림을
        /// 10x10 상자에 넣어 버리는데 글자 폭은 11로 적어 놔서, TMP가 10칸 그림을 11칸에
        /// 늘려 그린다. 그러면 획 한 줄이 두 배로 두꺼워져 글자가 뭉개진다.
        /// 12로 구우면 11픽셀 그림이 11칸 상자에 그대로 들어가고 글자 사이가 1칸 벌어진다.
        /// </summary>
        public int BakeSize;

        /// <summary>
        /// 참이면 TMP 아틀라스를 안티에일리어싱 없이 굽고 Point로 샘플링한다.
        /// 픽셀 폰트를 SDF로 구우면 획 가장자리가 회색으로 번져 11픽셀 격자가 무너진다.
        /// </summary>
        public bool Raster;
    }

    // 배열 순서는 FontChoice와 같아야 한다.
    private static readonly FontProfile[] FontProfiles =
    {
        new FontProfile
        {
            TtfPath = "Assets/Fonts/Galmuri11.ttf",
            TmpAssetPath = "Assets/Fonts/Galmuri11 Raster.asset",
            TmpAssetName = "Galmuri11 Raster",
            BakeSize = 12,
            Raster = true,
        },
        new FontProfile
        {
            TtfPath = "Assets/Fonts/ThinMulmaru Mono.ttf",
            TmpAssetPath = "Assets/Fonts/ThinMulmaru Mono SDF.asset",
            TmpAssetName = "ThinMulmaru Mono SDF",
            BakeSize = 90,
            Raster = false,
        },
    };

    private static FontProfile ActiveProfile => FontProfiles[(int)ActiveFont];

    // 글자 크기는 구운 크기의 정수배만 쓴다. 사이 값을 쓰면 아틀라스를 정수배가 아닌
    // 비율로 늘리게 되어 획 굵기가 들쭉날쭉해진다.
    //
    // 갈무리 7·9·11·14 를 각각 8·10·12·15 로 구워 두었으므로(FontBakes 참고)
    // 쓸 수 있는 크기는 8·10·12·15·16·20·24·30·32·36 … 이다.
    // 여기 없는 크기가 필요하면 그 목록의 배수 중에서 골라 아래에 추가하면 된다.
    // CreateTmpText 가 크기를 보고 알아서 맞는 폰트를 골라 단다.
    private const int TextTiny = 8;     // 갈무리7 x 1 — 주문서처럼 글이 아주 많은 곳
    private const int TextSmall = 10;   // 갈무리9 x 1 — 글이 많은 곳
    private const int TextBody = 12;    // 갈무리11 x 1 — 본문, 버튼, 상단바, 대사
    private const int TextHead = 16;    // 갈무리7 x 2 — 소제목
    private const int TextTitle = 24;   // 갈무리11 x 2 — 팝업 제목

    private const string BowlDir = "Assets/Art/그릇/";
    private const string EtcDir = "Assets/Art/나머지/";
    private const string IngredientDir = "Assets/Art/재료/";
    private const string UiDir = "Assets/Art/UI/";
    private const string ScreenDir = "Assets/Art/화면/";

    /// <summary>타래 3통과 향미유통이 기본·선택 두 줄로 함께 들어 있는 한 장. 자르기 영역은 아래 참고.</summary>
    private const string TareSheet = IngredientDir + "타레통_향미유통.png";

    /// <summary>코드로 만들어 둔 임시 그림. 기획자 그림이 오면 같은 이름으로 덮어쓰면 된다.</summary>
    private const string GeneratedDir = "Assets/Art/UI/Generated/";

    /// <summary>
    /// 모든 그림이 같은 값을 써야 하는 기준. 캔버스의 referencePixelsPerUnit 과 같은 100이다.
    ///
    /// 이 둘이 어긋나면 Image 가 그림을 그 비율만큼 확대·축소한다. 100 대신 1을 넣었더니
    /// 나무 타일 한 장이 6400칸이 되어 화면 전체가 널판 한 장 안쪽만 보였다.
    /// </summary>
    private const float SpritePixelsPerUnit = 100f;


    /// <summary>
    /// 기준 격자. 화면을 이 칸 수 안에서 짠다. 여기 좌표 1칸이 픽셀아트 1픽셀이다.
    /// 화면에는 PixelPerfectCanvas가 정수 배율(1920x1080이면 3배)로 띄운다.
    /// </summary>
    private static readonly Vector2 DesignResolution = new Vector2(640f, 360f);

    /// <summary>
    /// 팝업 뒤를 어둡게 덮는 판의 크기. 판(640×360)보다 넉넉히 크게 잡아
    /// 16:9가 아닌 창에서 생기는 여백까지 덮게 한다. 어차피 단색이라 커도 손해가 없다.
    /// </summary>
    private static readonly Vector2 ScreenCover = new Vector2(1920f, 1080f);

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

    // 크기는 전부 원본 PNG의 픽셀 수 그대로다. 기준 격자 1칸 = 원본 1픽셀이므로
    // 배율을 곱할 자리가 없다. 여기에 1.5배 같은 값이 끼면 그 순간 픽셀이 깨진다.
    private static readonly Vector2 BinSize = new Vector2(64f, 64f);           // 재료통·타래통·조미료병 원본 64px
    private static readonly Vector2 PotSize = new Vector2(64f, 64f);           // 육수 냄비 원본 64px
    // 면 튀김기 원본 84px. 예전에는 128 그림을 1920 판에서 2배(256)로 놓았는데, 다른 그림은
    // 전부 3배였다. 즉 튀김기만 일부러 2/3로 줄여 쓰고 있었다. 1:1 판으로 옮기면서 그 축소가
    // 사라져 튀김기만 1.5배 커졌다. 그래서 128을 2/3인 85로 다시 찍되, 홀수면 두 대를 붙일 때
    // 가장자리가 반칸에 걸리므로 짝수인 84로 맞췄다.
    private static readonly Vector2 NoodleBinSize = new Vector2(84f, 84f);

    // 그릇만 예외로 2배다. 여기가 화면의 주인공이고, 1배로 두면 재료통과 같은 크기라
    // 라멘을 만드는 화면인지 알 수 없다. 좌표계를 1:1로 바꾸기 전에도 그릇만 6배(나머지 3배)였다.
    // 2배는 정수배라 픽셀이 깨지지 않는다. 3배(384)는 화면 세로 360을 넘는다.
    // 이 값을 바꾸면 Bowl.BowlPixelScale도 같이 맞춰야 재료가 그릇과 따로 논다.
    private const float BowlScale = 2f;

    /// <summary>
    /// 참이면 국물 수면 아래 재료를 잘라내 잠긴 것처럼 보이게 한다(AttachBrothClip).
    /// 지금은 꺼 둔 상태다. 켜려면 이 값을 true로 바꾸고 빌더를 다시 돌리면 된다.
    /// 그림(Art/그릇/국물수면.png)은 이미 만들어져 있다.
    /// const가 아니라 readonly인 이유는, const로 두면 꺼져 있을 때 "닿지 않는 코드" 경고가 뜨기 때문이다.
    /// </summary>
    private static readonly bool ClipUnderBroth = false;
    private static readonly Vector2 BowlSize = new Vector2(128f * BowlScale, 128f * BowlScale);

    // 주문 화면 대사창. 손님 대사 세 마디가 보이게 잡은 값이다.
    //
    // 대사는 19~25자라 폭 400px 안에서 거의 전부 두 줄을 먹는다. 한 마디를 한 줄에 담으려면
    // 폭이 649px 필요한데 말풍선이 560px이라 불가능하다. 그래서 "세 마디 = 여섯 줄"로 잡는다.
    // 말풍선 안에서 대사가 쓸 수 있는 세로 공간은 75칸이다.
    //
    // 갈무리의 기본 줄 높이는 글자 크기의 1.33배라 여섯 줄이면 96칸으로 넘친다. 그래서 줄
    // 간격을 글자 크기와 같은 12칸으로 좁힌다. 글자 상자가 11픽셀이라 12칸이면 1칸이 남는다.
    /// <summary>
    /// 대사 글자 크기. 갈무리14 를 15로 구운 것을 쓴다(FontBakes 참고).
    /// 본문 12(갈무리11)보다 한 단계 크다. 크기에 맞는 폰트는 CreateTmpText 가 알아서 고른다.
    /// </summary>
    private const float DialogueFontSize = 15f;
    /// <summary>
    /// 대사 줄 간격. 갈무리14를 15로 구웠을 때의 기본 줄 높이와 같은 값이다.
    /// 15로 좁혔더니 두 줄이 딱 붙어 답답했다. 폰트가 원래 잡아 둔 간격이 제일 편하다.
    /// </summary>
    private const float DialogueLineHeight = 20f;
    /// <summary>
    /// 대사창에 한 번에 보이는 줄 수.
    ///
    /// 네 줄이다. 대사창에는 최근 두 마디가 보인다 — 위는 지난 마디(회색), 아래는 방금 마디(검정).
    /// 한 마디가 최대 두 줄이므로 두 마디면 네 줄이다. OrderScreenUI 가 딱 두 마디만 넣으므로
    /// 옛 줄이 위에서 반 토막 난 채 걸리는 일이 없다.
    ///
    /// 창 높이는 줄 간격 x (줄수-1) + 첫 줄 상자 높이로 잡는다. 12의 배수로만 잡으면
    /// 맨 윗줄이 4칸 잘린다. 첫 줄은 글자 위아래 여백까지 들어가 16칸을 차지하기 때문이다.
    /// </summary>
    private const int DialogueVisibleLines = 4;

    /// <summary>말풍선 테두리와 글자 사이 여백. 사방 같은 값을 쓴다.</summary>
    private const float BubblePadding = 8f;

    /// <summary>
    /// 말풍선 글자가 쓸 수 있는 최대 폭.
    ///
    /// 말풍선 왼쪽 위가 화면 (1,47)에 고정이라, 폭이 224 를 넘으면 오른쪽 끝이 손님(256부터)을
    /// 파고든다. 224 + 여백 16 = 240 이 상자 최대 폭이고 오른쪽 끝이 241 이다.
    /// </summary>
    private const float BubbleMaxTextWidth = 224f;

    /// <summary>
    /// 말풍선 높이. 여백(위아래 8) + 글자 네 줄(80)이다.
    /// 네 줄은 두 마디가 각각 두 줄까지 접히는 최악의 경우다.
    ///
    /// 버튼 자리를 따로 잡아 두지 않는다. 넵 버튼은 오른쪽 아래 구석에 얹혀 있는데,
    /// 글이 거기까지 닿는 경우가 거의 없어 자리를 비워 두면 아래가 휑하게 남는다.
    /// </summary>
    private const float BubbleHeight = 96f;
    private static readonly Vector2 CursorSize = new Vector2(64f, 64f);        // 젓가락·국자 시트 프레임 원본 64px
    private static readonly Vector2 RippleSize = new Vector2(32f, 32f);        // 클릭 파문 원본 32px

    // 글자는 흰색으로 두고 검은 테두리를 둘러 배경 위에서 읽히게 한다.
    // 배경이 베이지 판과 45도 픽셀아트로 갈려서 단색 글자로는 한쪽에서 반드시 묻힌다.
    private static readonly Color InkColor = Color.white;
    private static readonly Color TextOutlineColor = Color.black;
    private static readonly Vector2 TextOutlineDistance = new Vector2(1f, -1f);

    /// <summary>
    /// 9-슬라이스 테두리를 원본의 몇 배로 늘릴지. 기준 격자가 곧 원본 픽셀이라 1이다.
    /// 이 값을 올리면 테두리만 굵어지고 나머지 그림과 픽셀 크기가 어긋난다.
    /// </summary>
    private const float PixelArtScale = 1f;

    // 이름표 판. 글자가 가장 긴 "목이버섯" 네 자가 12칸씩 48칸이고, 9-슬라이스 테두리가
    // 좌우 8칸씩이라 64가 딱 맞는 폭이다. 높이 16은 통 사이에 벌려 둔 간격과 같다.
    private static readonly Vector2 LabelBoxSize = new Vector2(64f, 16f);

    // 재료통 7종은 64x64 그림이지만 위아래 10칸이 투명 여백이다. 실제 통은 (2,10)에서 60x44다.
    // 유니티 스프라이트 좌표는 아래가 0이라, 그림 위에서 10칸 자른 자리가 y=10이 된다.
    // 김만 내용이 60x44고 나머지 여섯은 60x42라, 같은 영역으로 잘라도 전부 안에 들어오고
    // 통끼리 세로 정렬도 저절로 맞는다.
    private static readonly Rect IngredientBinCrop = new Rect(2f, 10f, 60f, 44f);
    private static readonly Vector2 IngredientBinSize = new Vector2(60f, 44f);

    // 타래 3통과 향미유통은 한 장(237x56)에 여덟 칸으로 그려져 있다.
    // 아래 줄이 기본, 위 줄이 테두리를 두른 선택 그림이고 테두리는 사방 1픽셀이다.
    // 유니티 스프라이트 좌표는 아래가 0이라, 그림 위쪽 줄(선택)이 y가 큰 쪽이다.
    //
    // 칸을 딱 맞는 그림 크기가 아니라 짝수로 끊었다. 향미유통은 그림이 25·27로 홀수라
    // 정수 자리에 놓으면 가장자리가 반칸에 걸린다. 위아래로 투명 한 줄을 더해 26·28로 맞추면
    // 기본과 선택이 같은 만큼 어긋나서 갈아 끼울 때 그림은 제자리에 있다.
    private static readonly Rect ShioTareCrop = new Rect(1f, 0f, 64f, 26f);
    private static readonly Rect ShoyuTareCrop = new Rect(72f, 0f, 64f, 26f);
    private static readonly Rect TonkotsuTareCrop = new Rect(141f, 0f, 64f, 26f);
    private static readonly Rect FlavorOilCrop = new Rect(208f, 0f, 28f, 26f);

    private static readonly Rect ShioTareHoverCrop = new Rect(0f, 28f, 66f, 28f);
    private static readonly Rect ShoyuTareHoverCrop = new Rect(71f, 28f, 66f, 28f);
    private static readonly Rect TonkotsuTareHoverCrop = new Rect(140f, 28f, 66f, 28f);
    private static readonly Rect FlavorOilHoverCrop = new Rect(207f, 28f, 30f, 28f);

    /// <summary>
    /// 끌고 다니는 그림의 기본 한 변. IngredientSlot 이 커서 크기(64)에 0.5를 곱해 쓰는 값과 같다.
    /// 그림 크기를 이 값으로 나눠 배율을 정한다.
    /// </summary>
    private const float GhostBaseSide = 32f;

    private static readonly Vector2 TareBinSize = new Vector2(64f, 26f);
    private static readonly Vector2 OilBinSize = new Vector2(28f, 26f);

    /// <summary>
    /// 눕혀 쓰는 이름표. 눕히면 화면에서 16 x 46으로 보인다.
    /// 46을 넘기면 맨 아래 재료통 이름표가 화면 밖으로 나간다. 통이 44고 마지막 통이
    /// 화면 밑변에 붙어 있어서, 통보다 긴 이름표는 갈 데가 없다.
    /// </summary>
    private static readonly Vector2 RotatedLabelBoxSize = new Vector2(46f, 16f);

    /// <summary>재료통 이름표를 통 오른쪽으로 밀어내는 거리. 통 30 + 간격 2 + 눕힌 이름표 8.</summary>
    private const float BinLabelOffsetX = 40f;

    /// <summary>육수 냄비. 원본 64를 2배로 놓는다. 와이어프레임에서 가장 큰 통이다.</summary>
    private static readonly Vector2 BrothPotSize = new Vector2(128f, 128f);

    /// <summary>타래 이름표를 통 오른쪽으로 밀어내는 거리. 통 32 + 간격 2 + 이름표 32.</summary>
    private const float TareLabelOffsetX = 66f;

    private const float PanelScale = 1f;
    private const float PanelBarHeight = 18f;

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

    /// <summary>주문 내역(Tab)에서 OrderNoteUI에 꽂아 줘야 하는 것들.</summary>
    private class OrderNoteRefs
    {
        public GameObject Root;
        public TextMeshProUGUI Dialogue;

        /// <summary>미끄러져 들어오는 종이. OrderNoteUI 가 이것만 움직인다.</summary>
        public RectTransform Paper;
    }

    /// <summary>레시피 책(B 키)에서 RecipeBookUI에 꽂아 줘야 하는 것들.</summary>
    private class RecipeBookRefs
    {
        public GameObject Root;
        public TextMeshProUGUI RecipeNames;
        public TextMeshProUGUI RecipeValues;
        public Button Close;

        /// <summary>아래에서 올라오는 판.</summary>
        public RectTransform Panel;
    }

    /// <summary>주문 화면에서 OrderScreenUI에 꽂아 줘야 하는 것들.</summary>
    private class OrderScreenRefs
    {
        public GameObject Root;
        public TextMeshProUGUI DayTime;
        public TextMeshProUGUI Revenue;
        public TextMeshProUGUI Dialogue;
        public RectTransform DialogueViewport;

        public Button Start;
        public Image StartImage;
        public TextMeshProUGUI StartLabel;
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

        /// <summary>참이면 이름표를 통 위에 붙인다. 화면 맨 아래에 닿는 통에만 쓴다.</summary>
        public readonly bool LabelAbove;

        /// <summary>
        /// 참이면 이름표를 90도 눕힌다. 와이어프레임의 오른쪽 재료통 이름표가 그 모양이다.
        /// 90도는 픽셀 격자를 그대로 보존하는 각도라 글자가 뭉개지지 않는다(45도는 안 된다).
        /// 눕히면 이름표가 가로로 16칸만 먹어서 화면 오른쪽이 그만큼 트인다.
        /// </summary>
        public readonly bool LabelRotated;

        /// <summary>
        /// 통 그림에서 잘라 쓸 영역. 폭이 0이면 그림 전체를 쓴다.
        ///
        /// 재료통 7종은 64x64 그림인데 위아래 10칸이 투명 여백이라 실제 통은 60x44뿐이다.
        /// 여백째 세우면 7개가 세로 448이 되어 화면(323)에 안 들어간다. 여백을 잘라내면
        /// 44 x 7 = 308로 들어간다. 그림을 다시 그리거나 줄일 필요가 없다.
        /// </summary>
        public readonly Rect CropRect;

        /// <summary>
        /// 마우스를 올렸을 때 갈아 끼울 그림의 자르기 영역. 폭이 0이면 갈아 끼우지 않고
        /// 통이 커지거나 밝아지는 기존 표시를 쓴다. BinPath 와 같은 파일에서 잘라낸다.
        /// </summary>
        public readonly Rect HoverCropRect;

        public SlotDef(IngredientType type, string label, Vector2 anchor, float x, float y, Vector2 size,
                       string binPath, string bowlPath = null, string dragPath = null, bool liquid = false,
                       float labelX = 0f, float labelY = 0f, string idSuffix = null, bool labelAbove = false,
                       Rect crop = default, bool labelRotated = false, Rect hoverCrop = default)
        {
            LabelRotated = labelRotated;
            CropRect = crop;
            HoverCropRect = hoverCrop;
            LabelAbove = labelAbove;
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
        // 640x360 판 안의 배치다. 좌표는 전부 원본 픽셀 단위고, 통은 원본 크기 그대로 놓는다.
        //
        // 자리 배분 (가로 640):
        //   왼쪽 8~72   타래 3통 + 육수 냄비를 세로 한 줄로
        //   가운데 128~400  그릇과 그 아래 면 튀김기
        //   오른쪽 480~612  재료통 7개를 두 열로
        // 세로는 상단바(? 버튼과 폐기 버튼의 아래끝 37) 밑으로 8칸 띄운 40부터 쓴다.
        // 붙여 놓으면 두 버튼이 바로 아래 통에 5칸씩 걸친다.
        //
        // 줄 간격은 80이다. 통이 64라 사이에 16칸이 남고, 그 자리에 이름표가 들어간다.
        // 통을 붙여 세우면(간격 64) 이름표를 놓을 데가 없어 아래 통을 덮는다.
        // 네 줄 x 80 = 320 이고 위쪽 40을 더하면 360 으로 화면에 딱 찬다.

        // 왼쪽 세로 줄: 타래 3통을 위에 붙여 세우고, 그 아래를 육수 냄비가 크게 차지한다.
        // 와이어프레임에서 육수는 왼쪽 아래를 통째로 쓰는 가장 큰 통이다.
        //
        // 타래는 이름표를 아래가 아니라 오른쪽에 붙여 통끼리 딱 붙였다(간격 64).
        // 아래에 붙이면 세 통이 240을 먹어 육수 자리가 안 나온다.
        new SlotDef(IngredientType.ShioTare,     "시오",     TopLeft, 40f, -72f, TareBinSize,
                    TareSheet, liquid: true,
                    crop: ShioTareCrop, hoverCrop: ShioTareHoverCrop),
        new SlotDef(IngredientType.ShoyuTare,    "쇼유",     TopLeft, 40f, -120f, TareBinSize,
                    TareSheet, liquid: true,
                    crop: ShoyuTareCrop, hoverCrop: ShoyuTareHoverCrop),
        new SlotDef(IngredientType.TonkotsuBase, "돈코츠",   TopLeft, 40f, -168f, TareBinSize,
                    TareSheet, liquid: true,
                    crop: TonkotsuTareCrop, hoverCrop: TonkotsuTareHoverCrop),

        // 육수 냄비. 원본 64를 2배로 놓아 왼쪽 아래를 채운다(화면 8~136 x 232~360).
        // 이름표는 와이어프레임처럼 냄비 안에 얹는다. 아래에 두면 화면 밖으로 나간다.
        new SlotDef(IngredientType.Broth,        "육수",     TopLeft, 72f, -296f, BrothPotSize,
                    EtcDir + "육수.png", liquid: true, labelY: -40f),

        // 가운데 아래: 면 튀김기 2대.
        // 두 그림은 한 대를 반으로 자른 것이다. 얇은면은 오른쪽 끝이, 굵은면은 왼쪽 끝이
        // 잘려 있어서 "얇은면 → 굵은면" 순서로 딱 붙여야 한 대로 이어진다. 순서를 바꾸면 갈라진다.
        // 이어지려면 간격이 통 너비(128)와 정확히 같아야 한다. 벌어지거나 겹치면 이음매가 보인다.
        //
        // 굵기는 채점에 반영된다. 라멘마다 기본 면이 정해져 있고 주문에 교체 요청이 섞인다.
        // 그릇 안 그림은 둘이 같다. 시트 마지막 프레임이 면을 그리므로 따로 얹지 않는다.
        // 이름표를 눕혀 오른쪽이 트인 만큼 튀김기를 그릇 쪽으로 당겼다.
        // 와이어프레임에서 면은 그릇 바로 아래 가운데에 있다.
        // 두 대의 간격은 통 너비(84)와 정확히 같아야 한 대로 이어진다. 중심 -42와 +42면
        // 가장자리가 -84 / 0 / +84로 전부 정수에 떨어지고, 그릇 한가운데 아래에 놓인다.
        // 세로는 84로 낮아진 만큼 아래에 붙여 화면 밑변(360)에 맞춘다.
        new SlotDef(IngredientType.ThinNoodles,  "얇은면",   Center, -42f, -138f, NoodleBinSize,
                    EtcDir + "얇은면.png", EtcDir + "얇은면 그릇용.png", labelAbove: true),
        new SlotDef(IngredientType.ThickNoodles, "굵은면",   Center, 42f, -138f, NoodleBinSize,
                    EtcDir + "굵은면.png", EtcDir + "굵은면 그릇용.png", labelAbove: true),

        // 조미료 2종. 튀김기와 재료통 사이에 세로로 둘을 세운다.
        // 와이어프레임처럼 이름표를 눕혀 병 왼쪽에 붙인다. 병이 좁아 아래에 두면 줄이 어긋난다.
        // 클릭하면 젓가락 대신 병 자체를 들고, 그릇에 대면 기울여 뿌린다.
        // 그릇용 그림이 없어 수량만 세고 그릇에는 안 나온다.
        new SlotDef(IngredientType.FlavorOil,    "향미유",   Center, 164f, -84f, OilBinSize,
                    TareSheet, liquid: true, labelX: -38f, labelRotated: true,
                    crop: FlavorOilCrop, hoverCrop: FlavorOilHoverCrop),
        new SlotDef(IngredientType.ChiliPowder,  "시치미",   Center, 164f, -148f, BinSize,
                    EtcDir + "시치미.png", liquid: true, labelX: -38f, labelRotated: true),

        // 오른쪽: 재료통 7개를 세로 한 줄로. 기획서 와이어프레임(3.3, PDF 8쪽) 배치다.
        //
        // 예전에는 두 열(4+3)이었다. 64짜리 통 7개면 448이라 화면에 안 들어간다고 봤기 때문인데,
        // 그림 파일이 64일 뿐 실제 통은 60x44고 위아래 10칸이 투명 여백이었다. 여백을 잘라내면
        // 44 x 7 = 308로 한 줄에 들어간다. 그림을 다시 그릴 필요가 없다.
        //
        //   쓸 수 있는 세로   상단바 37 아래부터 360까지 = 323
        //   피치 46           6 x 46 + 44 = 320 (위 2 아래 1 여유)
        //
        // 피치를 44(간격 0)까지 좁힐 수 있지만 46으로 둔다. SlotHover가 통을 1.08배로 키우는데
        // 44 x 1.08 = 47.5라 위아래로 1.75씩 커진다. 간격 2가 그걸 받아 주는 최소선이다.
        //
        // 이름표는 통 아래가 아니라 오른쪽에 붙인다. 간격이 2뿐이라 아래에 둘 자리가 없다.
        new SlotDef(IngredientType.Menma,        "멘마",     MidRight, -90f, 119f, IngredientBinSize,
                    IngredientDir + "멘마 재료통.png", IngredientDir + "멘마 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
        new SlotDef(IngredientType.Chashu,       "차슈",     MidRight, -90f, 73f, IngredientBinSize,
                    IngredientDir + "차슈 재료통.png", IngredientDir + "차슈 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
        new SlotDef(IngredientType.GreenOnion,   "파",       MidRight, -90f, 27f, IngredientBinSize,
                    IngredientDir + "파 재료통.png", IngredientDir + "파 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
        new SlotDef(IngredientType.Nori,         "김",       MidRight, -90f, -19f, IngredientBinSize,
                    IngredientDir + "김 재료통.png", IngredientDir + "김 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
        new SlotDef(IngredientType.Egg,          "계란",     MidRight, -90f, -65f, IngredientBinSize,
                    IngredientDir + "계란 재료통.png", IngredientDir + "계란 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
        // 숙주만 파일 이름이 "테두리 강조"가 아니라 "테두리"다.
        new SlotDef(IngredientType.BeanSprout,   "숙주",     MidRight, -90f, -111f, IngredientBinSize,
                    IngredientDir + "숙주 재료통.png", IngredientDir + "숙주 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
        // 눕힌 이름표가 46칸뿐이라 네 글자는 안 들어간다. 와이어프레임도 "목이"로 줄여 적혀 있다.
        new SlotDef(IngredientType.WoodEar,      "목이",     MidRight, -90f, -157f, IngredientBinSize,
                    IngredientDir + "목이버섯 재료통.png", IngredientDir + "목이버섯 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
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

        Transform canvasRoot = CreateCanvas();
        EnsureEventSystem();
        EnsureGameManager();

        // 배경만 캔버스 전체를 덮는다. 16:9가 아닌 창에서 판 바깥이 비어 보이지 않게 하려는 것이다.
        BuildBackground(canvasRoot);

        // 나머지는 전부 640×360 판 안에 넣는다. 아래에서 canvas는 곧 그 판이다.
        Transform canvas = CreateFrame(canvasRoot);
        TopBarRefs topBar = BuildTopBar(canvas, font);
        // 팻말은 통보다 위에 그려져야 가려지지 않는다. 그리기 순서가 곧 만드는 순서라 먼저 만들 수 없어,
        // 오브젝트만 미리 만들어 슬롯에 넘기고 자리는 아래에서 옮긴다.
        SlotNameplate nameplate = BuildSlotNameplate(canvas, font);
        BuildSlots(CreateGroup("Slots", canvas), font, nameplate);

        Bowl bowl = BuildBowl(canvas);

        // 거부 안내는 그릇 위에 떠야 하므로 그릇보다 뒤에 만든다.
        bowl.toast = BuildIngredientToast(canvas);

        // 팻말을 여기서 맨 뒤로 보낸다. 통과 그릇보다는 위에, 팝업들보다는 아래에 있어야 한다.
        // 통보다 아래면 팻말이 통에 가리고, 팝업보다 위면 팝업 위에 이름이 떠 버린다.
        nameplate.transform.SetAsLastSibling();

        // 폐기 버튼은 그릇보다 먼저 만들어지므로 둘이 다 생긴 뒤에 연결한다.
        WireDiscardButton(topBar.Discard, bowl);

        // 정산 팝업. 커서보다 아래여야 하므로 DragLayer보다 먼저 만든다.
        ResultPopupRefs popup = BuildResultPopup(canvas);
        FinalPopupRefs finalPopup = BuildFinalPopup(canvas);

        // 주문 화면. 조리 화면을 통째로 덮으므로 팝업들보다 뒤에 만든다.
        OrderScreenRefs orderScreen = BuildOrderScreen(canvas);
        RecipeBookRefs recipeBook = BuildRecipeBook(canvas);
        OrderNoteRefs orderNote = BuildOrderNote(canvas);
        OrderResultRefs orderResult = BuildOrderResult(canvas);

        // 주문을 만들어 줄 B의 컴포넌트들을 씬에 올리고 GameManager와 잇는다.
        OrderSystemRefs orderSystem = EnsureOrderSystem(popup, finalPopup, orderScreen, recipeBook, orderResult, orderNote);
        WireGameManager(orderSystem, topBar);

        // DragLayer는 반드시 마지막. 그래야 드래그 고스트와 커서가 항상 모든 UI 위에 그려진다.
        // 이 그룹 자체에는 Image를 붙이지 않는다. 붙이면 화면 전체를 덮어 모든 클릭을 삼킨다.
        Transform dragLayer = CreateGroup("DragLayer", canvas);
        BuildClickRipple(dragLayer);

        // 주문 내역(Tab)은 빼 둔다. 그건 보면서 조리하는 창이라 젓가락이 그대로 있어야 한다.
        BuildCursor(dragLayer, new[]
        {
            popup.Root, finalPopup.Root, orderScreen.Root, recipeBook.Root, orderResult.Root
        });

        // 더티 표시만 하면 디스크 파일은 그대로라, 이 상태로 커밋하면 옛 씬이 올라간다.
        // 실제로 한 번 그렇게 커밋돼서 클론 시 주문 시스템이 없는 씬이 나갔다. 그래서 바로 저장한다.
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = canvasRoot.gameObject;
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

        // 그리는 자리를 픽셀 경계에 맞춰 끊는다. 글자는 줄 길이가 홀수면 반칸에서 시작하는데,
        // 반칸에 걸린 그림은 어떤 획은 3픽셀, 어떤 획은 4픽셀로 찍혀 굵기가 들쭉날쭉해진다.
        canvas.pixelPerfect = true;

        // 배율은 PixelPerfectCanvas가 창 크기를 보고 정수로 정한다. 스케일러 설정은 건드리지 않는다.
        var pixelPerfect = Undo.AddComponent<PixelPerfectCanvas>(go);
        pixelPerfect.referenceResolution = new Vector2Int((int)DesignResolution.x, (int)DesignResolution.y);

        return go.transform;
    }

    /// <summary>
    /// 화면을 짜는 640×360 판. 모든 UI가 이 안에 들어간다.
    ///
    /// 캔버스 자체는 창 비율을 그대로 받아서 16:9가 아니면 640×360보다 넓거나 높아진다.
    /// 그때 요소를 캔버스 가장자리에 직접 붙여 두면 창 크기마다 간격이 벌어져 구도가 흔들린다.
    /// 그래서 크기가 고정된 판을 하나 깔고 거기에 붙인다. 남는 자리는 여백이 된다.
    /// </summary>
    private static Transform CreateFrame(Transform canvas)
    {
        var rt = (RectTransform)CreateGroup("Frame", canvas);
        rt.anchorMin = Center;
        rt.anchorMax = Center;
        rt.pivot = Center;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = DesignResolution;
        return rt;
    }

    /// <summary>
    /// 조리 화면 배경. 포장마차 안쪽 나무 판이다.
    /// 판(640x360)이 아니라 창 전체를 덮으므로 배율이 정수로 떨어지지 않는다. LoadPhotoSprite 참고.
    /// 그림을 못 찾으면 예전 베이지 단색으로 돌아간다. 배경이 아예 없으면 요소들이 허공에 뜬다.
    /// </summary>
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

        // 조리 화면 바닥. 지금은 코드로 만든 나무 널판 타일을 시험 삼아 깔아 두었다.
        // 사진 배경(제조화면 배경.png)으로 되돌리려면 아래 두 줄의 순서를 바꾸면 된다.
        //
        // Tiled 는 그림을 원본 크기 그대로 반복해 찍으므로 늘어나지 않는다. 배율 1이면 64칸마다
        // 한 장이다. 임포트에서 wrap 이 Repeat, filter 가 Point 여야 이음매와 픽셀이 안 깨진다.
        Sprite wood = LoadSprite(GeneratedDir + "WoodCounter_64.png");
        if (wood != null)
        {
            img.sprite = wood;
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = Color.white;
        }
        else
        {
            Sprite art = LoadPhotoSprite(ScreenDir + "제조화면 배경.png");
            if (art != null)
            {
                img.sprite = art;
                img.color = Color.white;
            }
            else
            {
                img.color = Hex("#F5E9D0");
            }
        }

        img.raycastTarget = false;   // 배경이 클릭을 먹지 않도록
    }



    /// <summary>나중에 연결해야 하는 것들을 묶어 돌려준다.</summary>
    private static TopBarRefs BuildTopBar(Transform canvas, Font font)
    {
        Transform bar = CreateGroup("TopBar", canvas);
        Sprite panel = PanelSprite();

        IconSprites icons = LoadIconSprites();

        // 주문 확인 ? 버튼. 그림에 물음표가 들어 있어 글자를 따로 얹지 않는다.
        Image help = CreateImage("OrderCheckButton", bar, TopLeft, new Vector2(27f, -21f), new Vector2(32f, 32f),
                                 Color.white, icons.Help);

        // 날짜와 영업 시각. 왼쪽에 시계 아이콘이 붙은 판이라 글자를 그만큼 오른쪽으로 민다.
        Image day = CreateImage("DayPanel", bar, TopLeft, new Vector2(127f, -19f), new Vector2(100f, PanelBarHeight),
                                Color.white, TimeBarSprite(), PanelScale);
        AttachPanelIcon(day, TimeIconSprite(), new Vector2(20f, 18f));
        Text dayText = CreateText("Label", day.transform, Center, Vector2.zero,
                                  new Vector2(100f - 13f, 15f), "1일차", TextBody, PopupInkColor, font,
                                  TextAnchor.MiddleCenter);

        // 제출 영역. 와이어프레임의 회색 가로 바.
        Image submit = CreateImage("SubmitZone", bar, TopCenter, new Vector2(0f, -18f), new Vector2(187f, 27f),
                                   Color.white, icons.SubmitBar);
        Undo.AddComponent<SubmitZone>(submit.gameObject);
        CreateLabel(submit.transform, "제출하기", TextBody, PopupInkColor, font);

        // 누적 매출. 재료비와 자본은 기획 확정으로 제거되어 누적 매출만 표시한다.
        Image revenue = CreateImage("RevenuePanel", bar, TopRight, new Vector2(-110f, -19f), new Vector2(113f, PanelBarHeight),
                                    Color.white, MoneyBarSprite(), PanelScale);
        AttachPanelIcon(revenue, MoneyIconSprite(), new Vector2(18f, 14f));
        Text revenueText = CreateText("Label", revenue.transform, Center, Vector2.zero,
                                      new Vector2(113f - 13f, 15f), "누적 수익 : 0₩", TextBody, PopupInkColor, font,
                                      TextAnchor.MiddleCenter);

        // 폐기 버튼. onClick은 그릇이 생긴 뒤 WireDiscardButton에서 붙인다.
        Image discard = CreateImage("DiscardButton", bar, TopRight, new Vector2(-23f, -21f), new Vector2(32f, 32f),
                                    Color.white, icons.Trash);

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
        var backdrop = CreateImage("Backdrop", root, Center, Vector2.zero, ScreenCover, new Color(0f, 0f, 0f, 0.6f));
        backdrop.raycastTarget = true;

        Image panel = CreateImage("Panel", root, Center, Vector2.zero, new Vector2(253f, 153f),
                                  Hex("#FFF8E7"), PanelSprite());

        var title = CreateTmpText("TitleText", panel.transform, Center, new Vector2(0f, 50f),
                                  new Vector2(233f, 27f), "Day 1 정산", TextTitle, tmpFont);
        var profit = CreateTmpText("ProfitText", panel.transform, Center, new Vector2(0f, 13f),
                                   new Vector2(233f, 20f), "당일 총 수익 : 0원", TextBody, tmpFont);
        var accuracy = CreateTmpText("AverageAccuracyText", panel.transform, Center, new Vector2(0f, -10f),
                                     new Vector2(233f, 20f), "평균 정확도 : 0.0%", TextBody, tmpFont);

        Image confirmImage = CreateImage("ConfirmButton", panel.transform, Center, new Vector2(0f, -50f),
                                         new Vector2(87f, 27f), Hex("#7BB661"), PanelSprite());
        var confirm = Undo.AddComponent<Button>(confirmImage.gameObject);
        confirm.targetGraphic = confirmImage;
        StyleButton(confirm);
        CreateTmpText("Label", confirmImage.transform, Center, Vector2.zero,
                      new Vector2(80f, 20f), "확인", TextBody, tmpFont);

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
        IconSprites icons = LoadIconSprites();

        Transform root = CreateGroup("OrderResult", canvas);

        var backdrop = CreateImage("Backdrop", root, Center, Vector2.zero, ScreenCover, new Color(0f, 0f, 0f, 0.7f));
        backdrop.raycastTarget = true;

        Image panel = CreateImage("Panel", root, Center, Vector2.zero, new Vector2(300f, 187f),
                                  Hex("#FFF8E7"), PanelSprite());

        Image accuracyBar = CreateImage("AccuracyBar", panel.transform, Center, new Vector2(0f, 63f),
                                        new Vector2(220f, 30f), Color.white, icons.AccuracyBar);
        var accuracy = CreateTmpText("AccuracyText", accuracyBar.transform, Center, Vector2.zero,
                                     new Vector2(207f, 27f), "정확도 : 0%", TextTitle, tmpFont);

        // 이모지는 아이콘 아틀라스에서 잘라 쓴다. 표정은 OrderResultUI가 정확도로 고른다.
        Sprite[] faces = icons.Faces;
        Image emoji = CreateImage("Emoji", panel.transform, Center, new Vector2(0f, 20f),
                                  new Vector2(48f, 48f), Color.white,
                                  faces != null && faces.Length > 0 ? faces[0] : null);
        emoji.preserveAspect = true;

        var line = CreateTmpText("CustomerLine", panel.transform, Center, new Vector2(0f, -20f),
                                 new Vector2(273f, 20f), "잘 먹었습니다.", TextBody, tmpFont);

        var reward = CreateTmpText("RewardText", panel.transform, Center, new Vector2(-67f, -50f),
                                   new Vector2(127f, 20f), "+ 0₩", TextBody, tmpFont);
        var revenue = CreateTmpText("RevenueText", panel.transform, Center, new Vector2(67f, -50f),
                                    new Vector2(133f, 20f), "누적 수익 : 0₩", TextBody, tmpFont);

        Image confirmImage = CreateImage("ConfirmButton", panel.transform, Center, new Vector2(0f, -77f),
                                         new Vector2(87f, 25f), Hex("#7BB661"), PanelSprite());
        var confirm = Undo.AddComponent<Button>(confirmImage.gameObject);
        confirm.targetGraphic = confirmImage;
        StyleButton(confirm);
        var confirmLabel = CreateTmpText("Label", confirmImage.transform, Center, Vector2.zero,
                                         new Vector2(80f, 20f), "확인", TextBody, tmpFont);
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

    /// <summary>Icon.png에서 잘라 낸 조각들.</summary>
    private class IconSprites
    {
        public Sprite SubmitBar;    // 초록 바 — 제출하기 영역
        public Sprite Trash;        // 휴지통 — 폐기 버튼
        public Sprite AccuracyBar;  // 살색 바 — 정확도 표시
        public Sprite Help;         // 물음표 — 주문 확인 버튼
        public Sprite[] Faces;      // 웃음 · 무표정 · 화남
        public Sprite Wood;         // 나무 바 — 시작 버튼
        public Sprite WoodPressed;  // 나무 바(눌린 모양)
    }

    /// <summary>
    /// Icon.png를 잘라 온다. 균일 격자가 아니라 칸을 하나씩 지정한다.
    /// 그림 좌표는 위에서 아래, 유니티 텍스처 좌표는 아래에서 위라 y를 뒤집어 적었다.
    /// 가로로 늘어나는 바에는 9-슬라이스 테두리를 줘서 끝 모양이 뭉개지지 않게 한다.
    /// </summary>
    private static IconSprites LoadIconSprites()
    {
        const string path = UiDir + "Icon.png";

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (importer == null || texture == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 아이콘 아틀라스를 찾지 못했습니다: " + path);
            return new IconSprites { Faces = new Sprite[3] };
        }

        int h = texture.height;
        Vector4 barBorder = new Vector4(8f, 5f, 8f, 5f);

        string[] names = { "Icon_Submit", "Icon_Trash", "Icon_Accuracy", "Icon_Help",
                           "Face_0", "Face_1", "Face_2", "Icon_Wood", "Icon_WoodDown" };
        Rect[] rects =
        {
            new Rect(0f,  h - 16f,  48f, 16f),   // 초록 바
            new Rect(49f, h - 16f,  16f, 16f),   // 휴지통
            new Rect(0f,  h - 33f,  48f, 16f),   // 살색 바
            new Rect(49f, h - 33f,  16f, 16f),   // 물음표
            new Rect(0f,  h - 50f,  16f, 16f),   // 웃음
            new Rect(17f, h - 50f,  16f, 16f),   // 무표정
            new Rect(34f, h - 50f,  16f, 16f),   // 화남
            new Rect(0f,  h - 67f,  46f, 16f),   // 나무 바
            new Rect(0f,  h - 84f,  46f, 16f)    // 나무 바(눌림)
        };
        Vector4[] borders =
        {
            barBorder, Vector4.zero, barBorder, Vector4.zero,
            Vector4.zero, Vector4.zero, Vector4.zero,
            barBorder, barBorder
        };

        // 칸 좌표까지 비교해야 한다. 개수만 보면 좌표를 고쳐도 다시 자르지 않는다.
        bool needsSlice = importer.spriteImportMode != SpriteImportMode.Multiple
                          || importer.spritesheet == null
                          || importer.spritesheet.Length != names.Length
                          || importer.spritesheet[0].rect != rects[0];

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

            var slices = new SpriteMetaData[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                slices[i] = new SpriteMetaData
                {
                    name = names[i],
                    rect = rects[i],
                    border = borders[i],
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };
            }

            importer.spritesheet = slices;
            importer.SaveAndReimport();
        }

        var found = new System.Collections.Generic.Dictionary<string, Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var sprite = asset as Sprite;
            if (sprite != null) found[sprite.name] = sprite;
        }

        System.Func<string, Sprite> pick = n =>
        {
            Sprite v;
            return found.TryGetValue(n, out v) ? v : null;
        };

        return new IconSprites
        {
            SubmitBar = pick("Icon_Submit"),
            Trash = pick("Icon_Trash"),
            AccuracyBar = pick("Icon_Accuracy"),
            Help = pick("Icon_Help"),
            Faces = new[] { pick("Face_0"), pick("Face_1"), pick("Face_2") },
            Wood = pick("Icon_Wood"),
            WoodPressed = pick("Icon_WoodDown")
        };
    }

    /// <summary>
    /// 늘려 쓰는 낱장 그림. 9-슬라이스 테두리를 줘서 크기를 바꿔도 모서리가 뭉개지지 않는다.
    /// </summary>
    private static Sprite LoadSlicedSprite(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 그림을 찾지 못했습니다: " + path);
            return null;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single
            || importer.spriteBorder != border
            || importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.spritePixelsPerUnit != SpritePixelsPerUnit)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = SpritePixelsPerUnit;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// Tab으로 여는 주문 내역. 영수증 그림 위에 손님 대사를 그대로 얹는다.
    /// 조리를 막지 않도록 뒷판을 두지 않는다. 보면서 재료를 넣을 수 있어야 한다.
    /// </summary>
    private static OrderNoteRefs BuildOrderNote(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("OrderNote", canvas);

        // 64x64 그림 안에 영수증이 40x49로 들어 있다. 여백째 늘리면 종이가 뭉개지므로 잘라 쓴다.
        // 9-슬라이스는 쓰지 않는다. 위쪽 밑줄과 테두리가 가로로 늘어나 뭉개진다.
        // 대신 원본 비율(40:49)을 지킨 채 통째로 확대한다.
        Sprite paper = LoadCroppedSprite(UiDir + "Order_history.png", "OrderPaper",
                                         new Rect(13f, 6f, 40f, 49f), Vector4.zero);

        // 크기는 원본(40x49)의 정수배여야 한다. 3.5배 같은 값을 쓰면 종이 테두리가
        // 어떤 줄은 3픽셀, 어떤 줄은 4픽셀이 되어 가장자리가 울퉁불퉁해진다.
        //
        // 6배(240x294)다.
        //
        // 본문 글자를 10칸으로 올리면서 종이도 같이 키워야 했다. 5배(200x245)로는 글자 10칸이
        // 물리적으로 안 들어간다 — 주문 400건을 재 보니 가장 긴 것이 161칸인데 종이가 내주는
        // 세로가 155칸뿐이었다. 8칸 글자로 내리면 5배로도 되지만 그때는 글씨가 너무 작았다.
        // 자리는 OrderNoteUI 가 정한다(숨은 자리 -440, 나온 자리 -40). 여기서는 숨은 자리로 둔다.
        // 왼쪽 바깥에서 미끄러져 들어와 화면 한가운데보다 조금 왼쪽에 선다.
        Image sheet = CreateImage("Paper", root, Center, new Vector2(-440f, 0f),
                                  new Vector2(240f, 294f), Color.white, paper);
        // 스프라이트에 옛 9-슬라이스 테두리 값(6,10,6,14)이 남아 있어 CreateImage가 Sliced를 고른다.
        // 그대로 두면 머리글이 있는 위쪽 14픽셀 띠만 3배로 눌리고 가운데만 늘어나 글자가 뭉개진다.
        // 여기는 통째로 균일 확대해야 하므로 Simple로 되돌린다.
        sheet.type = Image.Type.Simple;
        sheet.preserveAspect = true;
        sheet.raycastTarget = false;

        // 머리글은 그림에서 지웠다. 64px 그림에 박힌 픽셀 글자를 10.5배로 늘리면 획이 뭉개진다.
        // 원본 글자가 있던 자리(종이 왼쪽 끝에서 31px, 위에서 21~105px)에 같은 폰트로 다시 쓴다.
        var header = CreateTmpText("HeaderText", sheet.transform, TopLeft, new Vector2(98f, -36f),
                                   new Vector2(136f, 28f), "주문서", TextHead, tmpFont);
        header.alignment = TextAlignmentOptions.Left;

        // 대사는 머리글 밑줄(위에서 115px)과 합계 줄(위에서 399px) 사이에만 놓는다.
        // 그 구간의 한가운데가 종이 정중앙이라 좌표는 0이다.
        // 머리글 밑줄과 합계 줄 사이(종이 높이의 22~77%)가 대사 자리다.
        // 글자 자리는 종이 그림에 그려진 줄에 맞춰 잡았다. 원본 49줄짜리 종이에서
        // 머리글 밑줄이 9번째, 아래 합계 줄이 40번째다. 6배로 띄우면 종이 위에서 54칸과 240칸이고,
        // 종이 한가운데를 0으로 보면 그 사이가 y -93 ~ +93, 즉 186칸이다.
        //
        // 상자를 그 안에 꽉 채워 위쪽 여백을 없앤다. 예전에는 상자가 band 보다 작아
        // 밑줄과 첫 줄 사이가 벌어져 보였다.
        var dialogue = CreateTmpText("DialogueText", sheet.transform, Center, Vector2.zero,
                                     new Vector2(200f, 186f), "", TextSmall, tmpFont);
        dialogue.alignment = TextAlignmentOptions.TopLeft;

        // 줄 간격 11칸. 글자(10칸)에 딱 맞춰 좁히면 줄이 붙어 읽기 나쁘고,
        // 12칸으로 벌리면 가장 긴 주문이 종이를 넘친다.
        ApplyPixelLineSpacing(dialogue, TextSmall + 1f);

        // 자동 크기 조절을 껐다. TMP가 16~26 사이에서 아무 값이나 골라 버리면
        // 11의 배수 규칙이 그 자리에서 깨져 글자에 회색이 낀다. 넘치면 줄이 아니라 판을 손본다.
        dialogue.enableAutoSizing = false;

        root.gameObject.SetActive(false);

        return new OrderNoteRefs { Root = root.gameObject, Dialogue = dialogue, Paper = sheet.rectTransform };
    }

    // 시간·수익 판은 막대와 아이콘을 따로 잘라 쓴다.
    // 한 장으로 9-슬라이스하면 아이콘이 판 높이에 묶여 작게만 나온다.
    // 막대는 늘어나도 되는 둥근 사각형이라 마음껏 늘리고, 아이콘은 원본 비율로 크게 얹는다.

    private static Sprite TimeBarSprite()
    {
        return LoadCroppedSprite(UiDir + "Time.png", "TimeBar",
                                 new Rect(19f, 27f, 37f, 16f), new Vector4(6f, 5f, 6f, 5f));
    }

    /// <summary>시계 아이콘만. 그림 좌표로 x9~18, y17~25. x8 칸은 막대의 왼쪽 테두리라 뺀다.</summary>
    private static Sprite TimeIconSprite()
    {
        return LoadCleanedIcon(UiDir + "Time.png", "TimeIcon", new RectInt(9, 38, 10, 9));
    }

    private static Sprite MoneyBarSprite()
    {
        return LoadCroppedSprite(UiDir + "Money_UI2.png", "MoneyBar",
                                 new Rect(17f, 16f, 40f, 20f), new Vector4(6f, 6f, 6f, 6f));
    }

    /// <summary>원 표시 배지만. 그림 좌표로 x8~16, y25~31. x7 칸은 막대의 왼쪽 테두리라 뺀다.</summary>
    private static Sprite MoneyIconSprite()
    {
        return LoadCleanedIcon(UiDir + "Money_UI2.png", "MoneyIcon", new RectInt(8, 32, 9, 7));
    }

    /// <summary>
    /// 판 그림에서 아이콘만 오려 내고, 뒤에 비치는 흰 막대를 지운 그림을 따로 만든다.
    ///
    /// 아이콘과 막대가 서로 물려 그려져 있어 사각형으로 자르면 흰 픽셀이 같이 딸려 온다.
    /// 아이콘을 막대 밖에 놓으면 그 흰색이 배경 위에 덩어리로 드러난다.
    /// 그래서 잘라낸 뒤 흰색만 투명으로 바꿔 Generated 폴더에 저장해 두고 그걸 쓴다.
    /// </summary>
    private static Sprite LoadCleanedIcon(string sourcePath, string outputName, RectInt area)
    {
        string folder = UiDir + "Generated";
        string outPath = folder + "/" + outputName + ".png";

        byte[] made = BuildCleanedIconBytes(sourcePath, area);
        if (made == null) return null;

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(UiDir.TrimEnd('/'), "Generated");
        }

        string full = System.IO.Path.GetFullPath(outPath);
        bool changed = !System.IO.File.Exists(full)
                       || !ByteArraysEqual(System.IO.File.ReadAllBytes(full), made);

        if (changed)
        {
            System.IO.File.WriteAllBytes(full, made);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
        }

        return LoadSprite(outPath);
    }

    /// <summary>잘라낸 조각에서 흰색을 투명으로 바꾼 PNG 바이트를 만든다.</summary>
    private static byte[] BuildCleanedIconBytes(string sourcePath, RectInt area)
    {
        var importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 그림을 찾지 못했습니다: " + sourcePath);
            return null;
        }

        // 픽셀을 읽으려면 읽기 허용이 켜져 있어야 한다.
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
        if (texture == null) return null;

        var pixels = texture.GetPixels(area.x, area.y, area.width, area.height);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            bool white = c.a > 0.1f && c.r > 0.9f && c.g > 0.9f && c.b > 0.9f;
            if (white) pixels[i] = new Color(0f, 0f, 0f, 0f);
        }

        var cut = new Texture2D(area.width, area.height, TextureFormat.RGBA32, false);
        cut.SetPixels(pixels);
        cut.Apply();

        byte[] png = cut.EncodeToPNG();
        Object.DestroyImmediate(cut);
        return png;
    }

    private static bool ByteArraysEqual(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    /// <summary>
    /// 판 왼쪽 바깥에 아이콘을 붙인다. 판 위에 겹쳐 놓으면 흰 막대가 아이콘 뒤로 삐져나온다.
    /// 이음매가 벌어지지 않도록 살짝만 물린다.
    /// </summary>
    private static void AttachPanelIcon(Image panel, Sprite icon, Vector2 size)
    {
        if (icon == null) return;

        const float overlap = 3f;
        // 반칸이 남으면 아이콘 전체가 픽셀 격자에서 반 칸 밀린다. 정수로 끊는다.
        float x = Mathf.Round(-(panel.rectTransform.sizeDelta.x + size.x) * 0.5f + overlap);

        Image image = CreateImage("Icon", panel.transform, Center, new Vector2(x, 0f), size, Color.white, icon);
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    /// <summary>
    /// 손님 대화창. 오른쪽에 꼬리가 달려 있어 따로 그리지 않는다.
    /// 9-슬라이스는 쓰지 않는다. 꼬리가 위쪽 테두리에 갇혀 구석에 작게 박힌다.
    /// 원본 비율(53:28)을 지킨 채 통째로 확대해야 꼬리가 제 위치에 제 크기로 나온다.
    /// </summary>
    /// <summary>
    /// 말풍선 본문. 꼬리를 뺀 사각형만 잘라 9-슬라이스로 쓴다.
    ///
    /// 통째로 5배 확대하면 1픽셀짜리 외곽선까지 5배가 되어 테두리가 뭉툭해진다.
    /// 9-슬라이스는 테두리를 원본 크기 그대로 찍고 가운데만 늘리므로, 판이 아무리 커져도
    /// 외곽선은 1픽셀로 남는다. 상단바 판(누적 수익)과 같은 방식이다.
    /// </summary>
    /// <summary>
    /// 손님 부위 그림을 번호 순서대로 불러온다. body_0 · head_0 처럼 이름이 붙어 있다.
    /// 중간에 빠진 번호가 있으면 거기서 멈춘다.
    /// </summary>
    private static Sprite[] LoadCustomerParts(string kind)
    {
        var list = new System.Collections.Generic.List<Sprite>();
        for (int i = 0; i < 16; i++)
        {
            string path = EtcDir + "Customer/" + kind + "_" + i + ".png";
            if (!System.IO.File.Exists(path)) break;

            Sprite sprite = LoadSprite(path);
            if (sprite == null) break;
            list.Add(sprite);
        }

        if (list.Count == 0)
            Debug.LogWarning("[RamenLayoutBuilder] 손님 " + kind + " 그림을 찾지 못했습니다: " + EtcDir + "Customer/");

        return list.ToArray();
    }

    private static Sprite SpeechBubbleBodySprite()
    {
        return LoadCroppedSprite(UiDir + "Order UI.png", "SpeechBubbleBody",
                                 new Rect(7f, 19f, 46f, 28f), new Vector4(4f, 4f, 4f, 4f));
    }

    /// <summary>
    /// 말풍선 꼬리. 본문 오른쪽에 따로 붙인다.
    ///
    /// 꼬리까지 9-슬라이스에 넣으면 세로로 늘어나 뭉개진다. 원본 7x5 를 그대로 두고
    /// 본문 오른쪽 끝에 얹는다.
    /// </summary>
    private static Sprite SpeechBubbleTailSprite()
    {
        return LoadCroppedSprite(UiDir + "Order UI.png", "SpeechBubbleTail",
                                 new Rect(53f, 34f, 7f, 5f), Vector4.zero);
    }

    private static Sprite SpeechBubbleSprite()
    {
        return LoadCroppedSprite(UiDir + "Order UI.png", "SpeechBubble",
                                 new Rect(7f, 19f, 53f, 28f), Vector4.zero);
    }

    /// <summary>
    /// 그림 안의 일부만 잘라 9-슬라이스로 쓴다.
    /// 낱장 스프라이트는 이미지 전체가 영역이라, 여백이 큰 그림은 늘리면 여백까지 늘어난다.
    /// </summary>
    private static Sprite LoadCroppedSprite(string path, string spriteName, Rect rect, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 그림을 찾지 못했습니다: " + path);
            return null;
        }

        // 한 파일에서 조각을 여럿 잘라 쓸 수 있어야 한다. 이미 있는 조각은 두고 필요한 것만 더한다.
        var slices = new System.Collections.Generic.List<SpriteMetaData>();
        if (importer.spriteImportMode == SpriteImportMode.Multiple && importer.spritesheet != null)
        {
            slices.AddRange(importer.spritesheet);
        }

        int found = slices.FindIndex(m => m.name == spriteName);
        bool needsSlice = found < 0 || slices[found].rect != rect || slices[found].border != border;

        if (needsSlice
            || importer.spriteImportMode != SpriteImportMode.Multiple
            || importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var meta = new SpriteMetaData
            {
                name = spriteName,
                rect = rect,
                border = border,
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };

            if (found >= 0) slices[found] = meta;
            else slices.Add(meta);

            importer.spritesheet = slices.ToArray();
            importer.SaveAndReimport();
        }

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var sprite = asset as Sprite;
            if (sprite != null && sprite.name == spriteName) return sprite;
        }

        Debug.LogWarning("[RamenLayoutBuilder] 잘라낸 스프라이트를 찾지 못했습니다: " + path);
        return null;
    }

    /// <summary>
    /// B 키로 여는 레시피 책. 주문 원문·기본 레시피·재료 속성표를 한 화면에 놓는다.
    /// 표는 이름 열과 내용 열을 따로 둔다. 한글은 글자 폭이 제각각이라 한 덩이 텍스트로는 줄이 안 맞는다.
    /// </summary>
    private static RecipeBookRefs BuildRecipeBook(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("RecipeBook", canvas);

        // 뒷배경을 어둡게 덮지 않는다. B를 누르고 있는 동안만 잠깐 올라오는 것이라
        // 화면을 가릴 이유가 없고, 조리하던 손이 끊긴다.

        // 기본 레시피 표 하나만 남아 판이 절반으로 줄었다. 재료 속성표는 사실상 정답지라 뺐다.
        //
        // 자리는 RecipeBookUI 가 정한다(숨은 자리 아래 -400, 올라온 자리 -20).
        // 여기서는 숨은 자리로 두고, B를 누르면 아래에서 미끄러져 올라온다.
        // 폭 320. 가장 긴 줄인 돈코츠(재료 여섯)가 10칸 글자로 약 250칸이라, 이름 열과 여백까지
        // 넣으면 이만큼 필요하다. 예전 360x150 에 12칸 글자로는 오른쪽으로 글이 삐져나왔다.
        Image panel = CreateImage("Panel", root, Center, new Vector2(0f, -400f), new Vector2(320f, 120f),
                                  Hex("#FFF8E7"), PanelSprite());

        CreateTmpText("Title", panel.transform, Center, new Vector2(0f, 42f),
                      new Vector2(200f, 20f), "기본 레시피", TextHead, tmpFont);

        // 이름 열은 오른쪽 정렬, 값 열은 왼쪽 정렬로 가운데에서 맞물린다.
        // 판 안쪽 폭은 좌우 여백 10을 뺀 300이고, 이름 40 + 값 260 으로 나눈다.
        var recipeNames = CreateTmpText("RecipeNames", panel.transform, Center, new Vector2(-130f, 6f),
                                        new Vector2(40f, 54f), "", TextSmall, tmpFont);
        recipeNames.alignment = TextAlignmentOptions.TopRight;
        var recipeValues = CreateTmpText("RecipeValues", panel.transform, Center, new Vector2(20f, 6f),
                                         new Vector2(260f, 54f), "", TextSmall, tmpFont);
        recipeValues.alignment = TextAlignmentOptions.TopLeft;

        // 줄바꿈이 생기면 왼쪽 이름 열과 줄이 어긋난다. 한 메뉴는 반드시 한 줄이어야 한다.
        recipeValues.textWrappingMode = TextWrappingModes.NoWrap;

        Image closeImage = CreateImage("CloseButton", panel.transform, Center, new Vector2(0f, -44f),
                                       new Vector2(70f, 20f), Hex("#7BB661"), PanelSprite());
        var close = Undo.AddComponent<Button>(closeImage.gameObject);
        close.targetGraphic = closeImage;
        StyleButton(close);
        var closeLabel = CreateTmpText("Label", closeImage.transform, Center, Vector2.zero,
                                       new Vector2(64f, 16f), "닫기", TextSmall, tmpFont);
        closeLabel.color = Color.white;

        root.gameObject.SetActive(false);

        return new RecipeBookRefs
        {
            Root = root.gameObject,
            RecipeNames = recipeNames,
            RecipeValues = recipeValues,
            Close = close,
            Panel = panel.rectTransform
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
        // 그림보다 크게 잡아 두어, 16:9가 아닌 창에서 판 바깥이 비어 보이지 않게 한다.
        var night = CreateImage("Night", root, Center, Vector2.zero, ScreenCover, DarkHex("#14100E"));
        night.raycastTarget = true;

        // 포장마차 그림. 노렌·뒷벽·카운터가 다 들어 있어서, 예전에 자리만 잡아 두었던
        // BackWall·CounterFront·CounterTop 색판 세 장을 이 한 장이 대신한다.
        // Night 와 달리 판(640x360)에 맞춘다. ScreenCover 로 두면 3배로 늘어나 가운데만 보인다.
        var scenery = CreateImage("Scenery", root, Center, Vector2.zero, new Vector2(640f, 360f),
                                  Color.white, LoadPhotoSprite(ScreenDir + "주문화면 배경.png"));
        scenery.raycastTarget = false;

        // 손님 자리.
        //
        // 손님은 카운터 "너머"에 서 있어야 한다. 예전에는 아래끝이 240이라 카운터 앞면(216.5)을
        // 23칸 파고들어, 사람이 상 위로 삐져나온 것처럼 보였다.
        // 지금은 상단바 아래(40)부터 카운터가 시작되는 216까지, 그 사이에만 들어가게 잡았다.
        //   화면 세로 40 ~ 216 = 176칸,  가운데는 128 → Center 기준 y = 180 - 128 = 52
        //
        // 얼굴과 몸통을 따로 얹는다. 손님이 바뀔 때마다 CustomerAppearance 가 무작위로 짝짓는다.
        // 그림 여덟 장(얼굴 4 + 몸통 4)으로 열여섯 가지가 나온다.
        var slot = (RectTransform)CreateGroup("CustomerSlot", root);
        slot.anchorMin = Center;
        slot.anchorMax = Center;
        slot.pivot = Center;
        slot.anchoredPosition = new Vector2(0f, 52f);
        slot.sizeDelta = new Vector2(126f, 176f);

        // 자리 밖으로 나간 부분을 잘라 낸다. 손님을 카운터 선 아래로 내려 두면
        // 여기서 잘려서 카운터 뒤에 서 있는 것처럼 보인다.
        Undo.AddComponent<RectMask2D>(slot.gameObject);

        // 몸통이 먼저(뒤에), 얼굴이 나중(앞에) 그려져야 목이 옷깃에 묻힌다.
        Image bodyImage = CreateImage("Body", slot, Center, Vector2.zero, new Vector2(94f, 118f), Color.white);
        Image headImage = CreateImage("Head", slot, Center, Vector2.zero, new Vector2(72f, 78f), Color.white);
        bodyImage.raycastTarget = false;
        headImage.raycastTarget = false;

        var look = Undo.AddComponent<CustomerAppearance>(slot.gameObject);
        SetPrivateReference(look, "bodyImage", bodyImage);
        SetPrivateReference(look, "headImage", headImage);
        SetPrivateArray(look, "bodies", LoadCustomerParts("body"));
        SetPrivateArray(look, "heads", LoadCustomerParts("head"));

        // 손님 대화창.
        //
        // 크기는 고정이다. 말 길이에 따라 늘였다 줄였다 하면 상자가 계속 들썩여 보인다.
        // 가장 긴 대사(두 마디 네 줄)가 들어가는 크기로 잡아 두고, 짧은 말은 가운데에 띄운다.
        //
        // 왼쪽 위 모서리를 기준으로 잡는다(피벗 0,1). 화면 (1, 47)이고 상단바 바로 아래,
        // 판 왼쪽 끝이다. 폭 240 은 오른쪽 끝이 241 이라 손님(256부터)을 안 건드리는 한계값이다.
        Image bubble = CreateImage("Bubble", root, Center, new Vector2(-319f, 133f),
                                   new Vector2(BubbleMaxTextWidth + BubblePadding * 2f, BubbleHeight),
                                   Color.white, SpeechBubbleBodySprite());
        bubble.rectTransform.pivot = new Vector2(0f, 1f);

        // 9-슬라이스로 그린다. 배율 1이라 테두리가 원본 그대로(얇게) 남는다.
        bubble.type = Image.Type.Sliced;
        bubble.pixelsPerUnitMultiplier = 1f;

        // 꼬리. 본문과 같은 배율(1배)로 둔다. 3배로 띄웠더니 꼬리 외곽선만 3픽셀이라
        // 본문의 1픽셀 테두리와 따로 놀았다.
        //
        // 오른쪽 변에 붙이고 위에서 44칸 내려 단다. 피벗이 왼쪽이라 x=0 이면 본문 변에 딱 붙는다.
        // 상자 높이가 변해도 위에서 잰 거리는 그대로라 꼬리가 늘 같은 자리에 있다.
        var tail = CreateImage("BubbleTail", bubble.transform, new Vector2(1f, 1f), new Vector2(0f, -44f),
                               new Vector2(7f, 5f), Color.white, SpeechBubbleTailSprite());
        tail.rectTransform.pivot = new Vector2(0f, 0.5f);
        tail.raycastTarget = false;

        // 대사는 페르소나·난이도에 따라 6~8마디까지 가는데, 클릭할 때마다 쌓인다(기획서 10.2).
        // 최근 세 마디만 보이고 넘친 옛 줄은 위로 밀려 사라져야 한다.
        //
        // 마스크는 반드시 이 빈 오브젝트에만 건다. 말풍선 그림에 걸면 꼬리까지 잘려 나간다.
        // 글자 자리는 상자 왼쪽 위에 붙인다. 상자가 자라도 여백이 그대로 유지된다.
        // 크기는 OrderScreenUI 가 대사 길이에 맞춰 매번 다시 잡는다.
        var viewport = (RectTransform)CreateGroup("Viewport", bubble.transform);
        viewport.anchorMin = new Vector2(0f, 1f);
        viewport.anchorMax = new Vector2(0f, 1f);
        viewport.pivot = new Vector2(0f, 1f);
        viewport.anchoredPosition = new Vector2(BubblePadding, -BubblePadding);
        // 가로: 글상자(152)보다 넓어야 한다. 마스크가 더 좁으면 첫 글자와 끝 글자의 바깥 획이 잘린다.
        // TMP가 글자 상자를 사방 1칸 넓게 잡으므로 좌우로 2칸씩 더 준다. 홀수면 경계가 반칸에 걸린다.
        //
        // 세로: 줄 간격 x (줄수-1) + 첫 줄 상자 높이다. 줄 간격(12)만으로 곱하면 안 된다.
        // 줄 간격은 줄과 줄 사이 거리고, 첫 줄은 글자 위아래 여백까지 들어가 16칸을 차지한다.
        // 그 4칸을 빼먹으면 맨 윗줄이 가로로 잘려 글자 윗부분이 날아간다.
        // 폭 212 는 말풍선 안에서 쓸 수 있는 최대에 가깝다. 말풍선 265 에서 오른쪽 꼬리(약 35)와
        // 좌우 테두리(8씩)를 빼면 214 가 남는다. 좁으면 긴 대사가 세 줄로 접혀 두 줄 창을 넘친다.
        viewport.sizeDelta = new Vector2(BubbleMaxTextWidth, Mathf.Round(
            DialogueLineHeight * (DialogueVisibleLines - 1) + FirstLineHeight(tmpFont, DialogueFontSize)));

        // 글상자는 창 한가운데에 둔다. 첫 마디는 말풍선 가운데에 뜨고, 마디가 늘면
        // 상자가 위아래로 함께 자라 앞 대사를 조금씩 위로 밀어낸다.
        // 상자가 창보다 길어지면 OrderScreenUI가 아래변을 창에 맞춰 붙여, 새 마디는 늘 보이고
        // 옛 마디가 창 위로 빠져나간다. 높이와 위치는 거기서 매 줄 다시 잡는다.
        //
        // 자동 축소는 켜지 않는다. 켜면 마디가 쌓일수록 글자가 작아져서
        // 첫 줄은 24pt, 여덟째 줄은 16pt인 화면이 된다.
        // 글상자는 창을 그대로 채우고, 글은 그 안에서 가운데에 놓인다.
        // 상자가 고정이라 짧은 말은 위나 왼쪽에 붙지 않고 한가운데에 뜬다.
        var dialogue = CreateTmpText("DialogueText", viewport, Center, Vector2.zero,
                                     viewport.sizeDelta, "손님을 기다리는 중...",
                                     DialogueFontSize, tmpFont);
        dialogue.alignment = TextAlignmentOptions.Center;
        ApplyPixelLineSpacing(dialogue, DialogueLineHeight);

        // 글자를 상자 안쪽으로 2칸 들여 쓴다.
        //
        // OrderScreenUI가 줄이 쌓일 때마다 글상자 폭을 마스크 폭과 똑같이 맞춰 버린다.
        // 그래서 마스크를 아무리 넓혀도 글상자가 같이 넓어져, 첫 글자와 끝 글자가
        // 마스크 경계에 딱 붙는다. 거기에 TMP가 글자 상자를 사방 1칸 넓게 잡으므로
        // 바깥 획이 잘려 나간다. 여백은 폭을 덮어써도 남으므로 여기서 막는다.
        dialogue.margin = new Vector4(2f, 0f, 2f, 0f);

        // 넘기기 버튼. 상자 오른쪽 아래 구석에 붙는다. 상자가 자라도 늘 구석에 있다.
        // 글자를 "▶"과 "넵"으로 줄여 버튼도 같이 작아졌다. 예전 112x30 은 상자의 절반을 먹었다.
        Image startImage = CreateImage("StartButton", bubble.transform, new Vector2(1f, 0f),
                                       new Vector2(-BubblePadding, BubblePadding),
                                       new Vector2(34f, 22f), Hex("#7BA7C7"),
                                       LoadSlicedSprite(UiDir + "TextBox.png", new Vector4(8f, 8f, 8f, 8f)));
        startImage.rectTransform.pivot = new Vector2(1f, 0f);
        startImage.type = Image.Type.Sliced;
        startImage.pixelsPerUnitMultiplier = 1f;

        var start = Undo.AddComponent<Button>(startImage.gameObject);
        start.targetGraphic = startImage;
        StyleButton(start);
        var startLabel = CreateTmpText("Label", startImage.transform, Center, Vector2.zero,
                                       new Vector2(30f, 18f), "▶", DialogueFontSize, tmpFont);
        startLabel.color = Color.white;

        Image dayPanel = CreateImage("DayTimePanel", root, TopLeft, new Vector2(140f, -21f),
                                     new Vector2(200f, PanelBarHeight), Color.white, TimeBarSprite(), PanelScale);
        AttachPanelIcon(dayPanel, TimeIconSprite(), new Vector2(20f, 18f));
        var dayTime = CreateTmpText("DayTimeText", dayPanel.transform, Center, Vector2.zero,
                                    new Vector2(187f, 19f), "영업 시간 1일차 / 17 : 00", TextBody, tmpFont);

        Image revenuePanel = CreateImage("RevenuePanel", root, TopRight, new Vector2(-87f, -21f),
                                         new Vector2(140f, PanelBarHeight), Color.white, MoneyBarSprite(), PanelScale);
        AttachPanelIcon(revenuePanel, MoneyIconSprite(), new Vector2(18f, 14f));
        var revenue = CreateTmpText("RevenueText", revenuePanel.transform, Center, Vector2.zero,
                                    new Vector2(127f, 19f), "누적 수익 : 0₩", TextBody, tmpFont);

        root.gameObject.SetActive(false);

        return new OrderScreenRefs
        {
            Root = root.gameObject,
            DayTime = dayTime,
            Revenue = revenue,
            Dialogue = dialogue,
            DialogueViewport = viewport,
            Start = start,
            StartImage = startImage,
            StartLabel = startLabel
        };
    }

    /// <summary>5일 영업이 끝났을 때 뜨는 최종 성적표. 정산 팝업과 같은 짜임새다.</summary>
    private static FinalPopupRefs BuildFinalPopup(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("FinalResultPopup", canvas);

        var backdrop = CreateImage("Backdrop", root, Center, Vector2.zero, ScreenCover, new Color(0f, 0f, 0f, 0.7f));
        backdrop.raycastTarget = true;

        Image panel = CreateImage("Panel", root, Center, Vector2.zero, new Vector2(273f, 187f),
                                  Hex("#FFF8E7"), PanelSprite());

        var title = CreateTmpText("TitleText", panel.transform, Center, new Vector2(0f, 65f),
                                  new Vector2(253f, 27f), "5일 영업 종료", TextTitle, tmpFont);
        var revenue = CreateTmpText("RevenueText", panel.transform, Center, new Vector2(0f, 27f),
                                    new Vector2(253f, 20f), "누적 매출 : 0원", TextBody, tmpFont);
        var accuracy = CreateTmpText("AccuracyText", panel.transform, Center, new Vector2(0f, 3f),
                                     new Vector2(253f, 20f), "평균 정확도 : 0.0%", TextBody, tmpFont);
        var perfect = CreateTmpText("PerfectText", panel.transform, Center, new Vector2(0f, -20f),
                                    new Vector2(253f, 20f), "완벽한 한 그릇 : 0 / 0건", TextBody, tmpFont);

        Image restartImage = CreateImage("RestartButton", panel.transform, Center, new Vector2(0f, -63f),
                                         new Vector2(100f, 28f), Hex("#7BB661"), PanelSprite());
        var restart = Undo.AddComponent<Button>(restartImage.gameObject);
        restart.targetGraphic = restartImage;
        StyleButton(restart);
        CreateTmpText("Label", restartImage.transform, Center, Vector2.zero,
                      new Vector2(93f, 20f), "다시 시작", TextBody, tmpFont);

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
    /// <summary>
    /// 팝업용 TMP 폰트 애셋. 없으면 활성 프로필의 ttf로 굽는다.
    ///
    /// 픽셀 폰트는 SDF로 구우면 안 된다. SDF는 획을 거리장으로 바꿔 가장자리를 부드럽게
    /// 되살리는 방식이라, 11픽셀 격자에 맞춰 그린 글자에 회색 테두리를 도로 입힌다.
    /// 그래서 프로필이 Raster면 안티에일리어싱 없는 래스터로 굽고 아틀라스를 Point로 샘플링한다.
    /// </summary>
    /// <summary>
    /// 크기별로 따로 구워 두는 원본 목록.
    ///
    /// 픽셀 폰트는 구운 크기의 정수배로만 또렷하다. 12로만 구워 두면 12·24·36 밖에 못 쓴다.
    /// 갈무리는 7·9·11·14 픽셀로 각각 따로 그려진 폰트가 있어서, 넷을 다 구워 두면
    /// 8·10·12·15·16·20·24·30·32·36… 으로 쓸 수 있는 크기가 촘촘해진다.
    ///
    /// 굽는 크기가 디자인 크기보다 1 큰 이유는 FreeType 때문이다. 디자인 크기 그대로 구우면
    /// 글자 그림을 한 칸 작은 상자에 넣으면서 글자 폭은 원래대로 적어 놔서, TMP 가 그림을
    /// 늘려 그린다. 1을 더하면 그림과 상자가 정확히 맞는다. 실측으로 확인한 값이다.
    /// </summary>
    private class FontBake
    {
        public string TtfPath;
        public string AssetPath;
        public string AssetName;
        public int BakeSize;
    }

    private static readonly FontBake[] FontBakes =
    {
        new FontBake { TtfPath = "Assets/Fonts/Galmuri7.ttf",  AssetPath = "Assets/Fonts/Galmuri7 Raster.asset",  AssetName = "Galmuri7 Raster",  BakeSize = 8 },
        new FontBake { TtfPath = "Assets/Fonts/Galmuri9.ttf",  AssetPath = "Assets/Fonts/Galmuri9 Raster.asset",  AssetName = "Galmuri9 Raster",  BakeSize = 10 },
        new FontBake { TtfPath = "Assets/Fonts/Galmuri11.ttf", AssetPath = "Assets/Fonts/Galmuri11 Raster.asset", AssetName = "Galmuri11 Raster", BakeSize = 12 },
        new FontBake { TtfPath = "Assets/Fonts/Galmuri14.ttf", AssetPath = "Assets/Fonts/Galmuri14 Raster.asset", AssetName = "Galmuri14 Raster", BakeSize = 15 },
    };

    /// <summary>
    /// 그 크기를 또렷하게 낼 수 있는 폰트를 고른다.
    ///
    /// 정수배로 딱 떨어지는 것 중 가장 큰 원본을 쓴다. 예를 들어 24 는 8·12 둘 다 되지만
    /// 12 쪽이 배율이 낮아(2배) 글자 모양이 원본에 가깝다.
    /// 딱 떨어지는 게 없으면 경고하고 기본 폰트로 넘긴다. 그 크기는 반드시 뭉갠다.
    /// </summary>
    private static TMP_FontAsset FontForSize(int size)
    {
        // 물마루로 갈아 끼운 상태에서는 이 사다리를 쓸 수 없다. 그때는 기본 폰트 하나뿐이다.
        if (!ActiveProfile.Raster) return EnsureTmpFont();

        FontBake best = null;
        foreach (FontBake bake in FontBakes)
        {
            if (size % bake.BakeSize != 0) continue;
            if (best == null || bake.BakeSize > best.BakeSize) best = bake;
        }

        if (best == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 글자 크기 " + size + "은(는) 또렷하게 낼 수 없습니다. " +
                             "쓸 수 있는 크기는 8·10·12·15 의 배수입니다(8, 10, 12, 15, 16, 20, 24, 30, 32, 36 …).");
            return EnsureTmpFont();
        }

        return EnsureBaked(best);
    }

    /// <summary>목록의 한 항목을 구워 둔다. 이미 같은 설정으로 있으면 그대로 쓴다.</summary>
    private static TMP_FontAsset EnsureBaked(FontBake bake)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(bake.AssetPath);
        if (existing != null)
        {
            if (existing.faceInfo.pointSize == bake.BakeSize && existing.atlasPadding == 1)
            {
                EnforcePointFilter(existing);
                return existing;
            }
            AssetDatabase.DeleteAsset(bake.AssetPath);
        }

        Font source = AssetDatabase.LoadAssetAtPath<Font>(bake.TtfPath);
        if (source == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] " + bake.TtfPath + " 을(를) 찾지 못했습니다.");
            return EnsureTmpFont();
        }

        EnforceRasterFontImport(bake.TtfPath);

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
            source, bake.BakeSize, 1, GlyphRenderMode.RASTER_HINTED, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (asset == null) return EnsureTmpFont();

        asset.name = bake.AssetName;
        AssetDatabase.CreateAsset(asset, bake.AssetPath);
        if (asset.atlasTextures != null && asset.atlasTextures.Length > 0)
        {
            asset.atlasTextures[0].name = bake.AssetName + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        }
        if (asset.material != null)
        {
            asset.material.name = bake.AssetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }
        EnforcePointFilter(asset);
        AssetDatabase.SaveAssets();
        Debug.Log("[RamenLayoutBuilder] 글자 크기용 폰트를 구웠습니다: " + bake.AssetPath + " (크기 " + bake.BakeSize + ")");
        return asset;
    }

    private static TMP_FontAsset EnsureTmpFont()
    {
        FontProfile profile = ActiveProfile;

        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(profile.TmpAssetPath);
        if (existing != null)
        {
            // 구운 크기가 지금 설정과 같을 때만 재사용한다. 크기를 고쳐도 옛 에셋이 그대로
            // 돌아오면 화면은 안 바뀌는데 코드만 바뀐 상태가 되어 원인을 못 찾는다.
            int wantPadding = profile.Raster ? 1 : 9;
            if (existing.faceInfo.pointSize == profile.BakeSize && existing.atlasPadding == wantPadding)
            {
                EnforcePointFilter(existing);
                return existing;
            }

            Debug.Log("[RamenLayoutBuilder] 굽는 설정이 바뀌어(크기 " + existing.faceInfo.pointSize +
                      "→" + profile.BakeSize + ", 여백 " + existing.atlasPadding + "→" + wantPadding +
                      ") TMP 폰트를 다시 굽습니다.");
            AssetDatabase.DeleteAsset(profile.TmpAssetPath);
        }

        Font source = AssetDatabase.LoadAssetAtPath<Font>(profile.TtfPath);
        if (source == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] " + profile.TtfPath + " 을(를) 찾지 못해 TMP 폰트를 만들지 못했습니다.");
            return null;
        }

        // 래스터는 글자를 원본 크기 그대로 굽고 화면에서 정수배로 늘린다. 그래서 샘플링 크기가
        // 곧 원본 픽셀 수다.
        //
        // 패딩을 0으로 두면 안 된다. TMP는 글자 상자를 사방 1칸씩 넓게 잡아 그리는데,
        // 아틀라스에 여백이 없으면 그 1칸이 바로 옆 글자를 물어 온다. 글자마다 좌우로
        // 남의 획이 1칸씩 딸려 나와 글자들이 겹쳐 보인다. 1칸을 비워 두면 그 자리가 투명해진다.
        int sampling = profile.BakeSize;
        int padding = profile.Raster ? 1 : 9;
        GlyphRenderMode mode = profile.Raster ? GlyphRenderMode.RASTER_HINTED : GlyphRenderMode.SDFAA;

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
            source, sampling, padding, mode, 1024, 1024, AtlasPopulationMode.Dynamic, true);

        if (asset == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] TMP 폰트 애셋 생성에 실패했습니다.");
            return null;
        }

        asset.name = profile.TmpAssetName;
        AssetDatabase.CreateAsset(asset, profile.TmpAssetPath);

        // 아틀라스 텍스처와 머티리얼을 같은 파일 안에 넣어야 참조가 끊기지 않는다.
        if (asset.atlasTextures != null && asset.atlasTextures.Length > 0)
        {
            asset.atlasTextures[0].name = profile.TmpAssetName + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        }
        if (asset.material != null)
        {
            asset.material.name = profile.TmpAssetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        EnforcePointFilter(asset);

        AssetDatabase.SaveAssets();
        Debug.Log("[RamenLayoutBuilder] TMP 폰트 애셋을 만들었습니다: " + profile.TmpAssetPath);
        return asset;
    }

    /// <summary>
    /// 래스터 아틀라스를 Point로 샘플링하게 한다. Bilinear로 두면 정수배로 늘려도
    /// 픽셀 사이가 섞여 회색이 낀다. 글자가 늘어나 아틀라스가 새로 생겨도 유지되도록
    /// 빌드할 때마다 다시 확인한다.
    /// </summary>
    private static void EnforcePointFilter(TMP_FontAsset asset)
    {
        if (!ActiveProfile.Raster) return;
        if (asset.atlasTextures == null) return;

        foreach (Texture2D atlas in asset.atlasTextures)
        {
            if (atlas != null) atlas.filterMode = FilterMode.Point;
        }
    }

    /// <summary>
    /// 글자 한 줄이 실제로 차지하는 상자 높이. 줄 간격과 다르다.
    /// 폰트가 글자 위아래에 두는 여백까지 포함한 값이라, 창 높이를 잡을 때는 이쪽을 써야 한다.
    /// </summary>
    private static float FirstLineHeight(TMP_FontAsset font, float fontSize)
    {
        if (font == null || font.faceInfo.pointSize <= 0) return fontSize;
        return font.faceInfo.lineHeight / font.faceInfo.pointSize * fontSize;
    }

    /// <summary>
    /// 줄 간격을 정확히 원하는 칸 수로 맞춘다.
    ///
    /// 갈무리는 글자가 11픽셀인데 기본 줄 높이가 14.67픽셀이다. 그대로 두면 줄마다
    /// 3.67칸씩 밀려 둘째 줄부터 글자가 픽셀 격자에서 벗어난다. 비트맵 폰트는 글자 상자
    /// 자체가 11픽셀이라 11칸으로 붙여도 위아래가 겹치지 않는다.
    ///
    /// TMP의 lineSpacing은 글자 크기에 대한 백분율이라 칸 수를 백분율로 환산해 넣는다.
    /// </summary>
    private static void ApplyPixelLineSpacing(TextMeshProUGUI text, float lineHeight)
    {
        if (text.font == null) return;

        UnityEngine.TextCore.FaceInfo face = text.font.faceInfo;
        if (face.pointSize <= 0) return;

        // 폰트가 알아서 벌리는 줄 높이를 글자 크기 기준으로 환산한 값.
        float natural = face.lineHeight / face.pointSize * text.fontSize;

        text.lineSpacing = (lineHeight - natural) / text.fontSize * 100f;
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

        // 글상자 폭이 홀수면 가운데 정렬한 글자가 반칸에서 시작한다(폭 151 -> 시작 -75.5).
        // 반칸에 걸린 글자는 픽셀 격자에서 벗어나 획 굵기가 들쭉날쭉해진다. 짝수로 끊는다.
        rt.sizeDelta = new Vector2(Mathf.Round(size.x * 0.5f) * 2f, Mathf.Round(size.y * 0.5f) * 2f);

        var text = go.GetComponent<TextMeshProUGUI>();

        // 넘겨받은 폰트 대신, 이 크기를 또렷하게 낼 수 있는 폰트로 바꿔 단다.
        // 크기마다 구운 원본이 달라서(8·10·12·15) 여기서 골라야 픽셀이 안 뭉갠다.
        TMP_FontAsset sized = FontForSize(Mathf.RoundToInt(fontSize));
        if (sized != null) text.font = sized;
        else if (font != null) text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = PopupInkColor;
        text.alignment = TextAlignmentOptions.Center;
        // 굵게를 쓰지 않는다. Unity의 Bold는 획을 인위적으로 부풀리는 처리라
        // 픽셀 폰트에 걸면 가장자리에 회색이 낀다. 갈무리는 획이 원래 2픽셀이라 그냥도 읽힌다.
        text.fontStyle = FontStyles.Normal;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// 통 아래(맨 아랫줄은 위)에 붙는 이름표. 통 사이에 벌려 둔 16칸이 이 판 자리다.
    /// </summary>
    private static void CreateSlotLabel(Transform bin, SlotDef def, Font font)
    {
        // 통 사이에 벌려 둔 16칸이 곧 이름표 높이라, 통 모서리에 판을 딱 붙이면 정확히 맞는다.
        // 여기에 여유를 더 주면 그만큼 아래 통을 파고들거나 화면 밖으로 밀린다.
        float offset = def.Size.y * 0.5f + LabelBoxSize.y * 0.5f;
        float y = def.LabelAbove ? offset : -offset;

        // 옆에 붙이라고 지정한 통은 위아래가 아니라 그 자리로 간다.
        // 재료통을 한 줄로 세우면 통 사이가 2칸뿐이라 아래에 이름표를 둘 자리가 없다.
        Vector2 where = def.LabelOffset.x != 0f || def.LabelOffset.y != 0f
            ? def.LabelOffset
            : new Vector2(0f, Mathf.Round(y));

        // 눕힌 이름표는 네 글자(목이버섯)가 들어가도록 조금 길게 잡는다.
        Vector2 size = def.LabelRotated ? RotatedLabelBoxSize : LabelBoxSize;

        Image box = CreateImage("LabelBox", bin, Center, where, size,
                                Hex("#FFF8E7"), LoadSlicedSprite(UiDir + "TextBox.png", new Vector4(8f, 8f, 8f, 8f)));

        // 이름표가 클릭을 먹으면 그 통의 드래그가 시작되지 않는다.
        box.raycastTarget = false;

        // 시계 방향으로 눕히면 글자가 위에서 아래로 읽힌다. 90도라 픽셀 격자는 그대로다.
        if (def.LabelRotated) box.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);

        CreateText("Label", box.transform, Center, Vector2.zero, size, def.Label,
                   TextBody, PopupInkColor, font, TextAnchor.MiddleCenter);
    }

    /// <summary>
    /// 재료통 이름 팻말. 판 전체에 하나만 두고 가리키는 통 옆으로 옮겨 쓴다.
    /// 통마다 하나씩 두면 판이 열다섯 개 깔려 그림을 가린다.
    /// </summary>
    private static SlotNameplate BuildSlotNameplate(Transform canvas, Font font)
    {
        // 글자가 가장 긴 "목이버섯" 네 자가 12칸씩 48칸이다. 좌우 여백을 8칸씩 둬 64로 잡는다.
        // 9-슬라이스 테두리가 8칸이라 이보다 좁으면 테두리끼리 겹친다.
        Image box = CreateImage("SlotNameplate", canvas, Center, Vector2.zero, new Vector2(64f, 20f),
                                Hex("#FFF8E7"), LoadSlicedSprite(UiDir + "TextBox.png", new Vector4(8f, 8f, 8f, 8f)));

        // 팻말이 클릭을 먹으면 그 아래 통의 드래그가 시작되지 않는다.
        box.raycastTarget = false;

        Text label = CreateText("Label", box.transform, Center, Vector2.zero, new Vector2(64f, 20f),
                                "", TextBody, PopupInkColor, font, TextAnchor.MiddleCenter);

        var plate = Undo.AddComponent<SlotNameplate>(box.gameObject);
        plate.label = label;
        return plate;
    }

    private static void BuildSlots(Transform parent, Font font, SlotNameplate nameplate)
    {
        foreach (SlotDef def in Slots)
        {
            // 자르기 영역이 있으면 그 부분만 떼어 쓴다. 재료통은 투명 여백을 잘라야 한 줄에 들어간다.
            Sprite binSprite = def.CropRect.width > 0f
                ? LoadCroppedSprite(def.BinPath, def.Type + (def.IdSuffix ?? "") + "Bin", def.CropRect, Vector4.zero)
                : LoadSprite(def.BinPath);

            Image bin = CreateImage("Slot_" + def.Type + (def.IdSuffix ?? ""), parent, def.Anchor, def.Pos, def.Size,
                                    Color.white, binSprite);
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

                // 끌고 다니는 그림 크기는 "그릇에 얹힐 그림"을 기준으로 잡는다.
                //
                // 통 크기로 잡으면 면이 너무 작아진다. 면 통은 84라 배율이 1로 반올림되는데,
                // 기본 크기가 커서(64) 곱하기 0.5인 32칸이라 면 한 덩이가 실오라기처럼 보였다.
                // 실제로 그릇에 들어갈 그림은 128칸짜리다. 그림 크기를 32로 나눠 배율을 잡으면
                // 면은 4배(128칸), 재료는 1배로 각자 제 크기가 나온다.
                //
                // 정수로 끊는 이유는 반픽셀에 걸리면 끌고 다니는 그림이 뭉개지기 때문이다.
                Sprite ghostSprite = slot.dragSprite != null ? slot.dragSprite : slot.bowlSprite;
                slot.ghostScale = ghostSprite != null
                    ? Mathf.Max(1f, Mathf.Round(ghostSprite.rect.width / GhostBaseSide))
                    : 1f;
            }

            // 커서가 젓가락 그림이라 어느 통을 가리키는지 알기 어렵다. 통이 직접 반응하게 한다.
            var hover = Undo.AddComponent<SlotHover>(bin.gameObject);
            hover.label = def.Label;

            // 테두리를 두른 그림이 따로 있는 통은 늘리는 대신 그림을 갈아 끼운다.
            // 테두리가 사방 1픽셀이라 상자도 그만큼 키워야 픽셀이 1:1로 떨어진다.
            if (def.HoverCropRect.width > 0f)
            {
                hover.hoverSprite = LoadCroppedSprite(def.BinPath, def.Type + (def.IdSuffix ?? "") + "BinHover",
                                                      def.HoverCropRect, Vector4.zero);
                hover.hoverSize = new Vector2(def.HoverCropRect.width, def.HoverCropRect.height);
            }

            // 이름표를 늘 띄우기로 해서 호버 팻말은 꽂지 않는다. 둘 다 켜면 같은 이름이
            // 두 군데 뜬다. 팻말로 되돌리려면 이 줄을 살리고 CreateSlotLabel 호출을 지운다.
            // hover.nameplate = nameplate;

            CreateSlotLabel(bin.transform, def, font);

            // 면 두 통은 한 대를 반으로 자른 그림이라 한쪽만 커지면 이음매가 벌어진다.
            // 대신 밝기로 표시하면 이어진 채로 어느 쪽을 가리키는지 구분된다.
            if (def.Type == IngredientType.ThickNoodles || def.Type == IngredientType.ThinNoodles)
            {
                hover.useScale = false;
            }
        }
    }

    /// <summary>시스템 커서를 대신하는 조리 도구. 평소 젓가락, 액체를 뜨면 국자.</summary>
    private static void BuildCursor(Transform dragLayer, GameObject[] uiScreens)
    {
        Sprite[] frames = LoadChopstickFrames();

        Image img = CreateImage("Cursor", dragLayer, Center, Vector2.zero, CursorSize,
                                Color.white, frames != null && frames.Length > 0 ? frames[0] : null);
        img.preserveAspect = true;

        // 커서가 레이캐스트를 먹으면 마우스 밑이 늘 커서라 아무것도 클릭할 수 없다.
        img.raycastTarget = false;

        var cursor = Undo.AddComponent<CookingCursor>(img.gameObject);
        cursor.chopstickFrames = frames;
        // 국자는 낱장이 아니라 30칸 시트다. 가로 5칸이 기울기, 세로 6줄이 담긴 것이고
        // 줄 순서는 CookingCursor.LadleRow 와 맞춰 두었다(빈·시오·쇼유·돈코츠·육수·향미유).
        // 칸이 80인 이유는 CookingCursor.LadleArtFrameSize 주석 참고.
        cursor.ladleFrames = LoadSpriteSheet(EtcDir + "국자 애니메이션.png", 80, 80);

        // 시치미 병 5칸. 국자와 달리 병은 64칸 안에서 다 돌아가서 칸을 키우지 않았다.
        // 세워 둔 24x56 병이 중심에서 30픽셀이라, 58도까지 기울여도 32를 안 넘는다.
        cursor.bottleFrames = LoadSpriteSheet(EtcDir + "시치미 애니메이션.png", 64, 64);

        // 메뉴 화면에서 쓰는 화살표. 끝점은 그림 왼쪽 위에서 2px 안쪽이다.
        cursor.uiScreens = uiScreens;
        cursor.arrowCursor = LoadCursorTexture(UiDir + "Cursor_Arrow.png");
        cursor.arrowCursorPressed = LoadCursorTexture(UiDir + "Cursor_Arrow_Press.png");
        cursor.arrowHotspot = new Vector2(2f, 2f);
    }

    /// <summary>
    /// 클릭한 자리에 퍼지는 링. 커서보다 먼저 만들어야 젓가락 아래에 깔린다.
    /// </summary>
    private static void BuildClickRipple(Transform dragLayer)
    {
        Sprite[] frames = LoadSpriteSheet(UiDir + "Click_Ring.png", 32, 32);

        Image img = CreateImage("ClickRipple", dragLayer, Center, Vector2.zero, RippleSize,
                                Color.white, frames.Length > 0 ? frames[0] : null);
        img.preserveAspect = true;
        img.raycastTarget = false;

        var ripple = Undo.AddComponent<ClickRipple>(img.gameObject);
        ripple.frames = frames;
    }

    /// <summary>
    /// 시스템 커서로 쓸 그림. Cursor 타입으로 들여와야 읽을 수 있고,
    /// 압축을 끄고 Point로 둬야 픽셀이 뭉개지지 않는다.
    /// </summary>
    private static Texture2D LoadCursorTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 커서 그림을 찾지 못했습니다: " + path);
            return null;
        }

        if (importer.textureType != TextureImporterType.Cursor
            || importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Cursor;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>
    /// 젓가락은 한 장에 64px 프레임 3개(벌림·중간·집음)가 가로로 붙어 있다.
    /// Multiple로 잘라 세 조각을 만든 뒤 왼쪽부터 순서대로 돌려준다.
    /// </summary>
    private static Sprite[] LoadChopstickFrames()
    {
        // 원본 젓가락.png는 3프레임이라 집는 동작이 뚝뚝 끊겼다.
        // 고정 짝을 복사해 손가락 잡는 지점을 축으로 돌린 4프레임을 따로 만들어 쓴다.
        const int frameCount = 4;
        const float frameSize = 64f;
        string path = EtcDir + "젓가락 애니메이션.png";

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
        // 상단바 아래와 면 튀김기 위 사이의 한가운데. 어느 한쪽에 치우치면 공중에 뜬 것처럼 보인다.
        Image bowl = CreateImage("Bowl", canvas, Center, new Vector2(0f, 40f), BowlSize,
                                 Color.white, LoadSprite(BowlDir + "빈그릇.png"));
        bowl.preserveAspect = true;

        Bowl component = Undo.AddComponent<Bowl>(bowl.gameObject);
        component.emptyBowlSprite = LoadSprite(BowlDir + "빈그릇.png");

        // 타래 종류마다 8프레임 시트가 한 장씩(4열 x 2행).
        // 0~3 타래 / 4~6 육수 / 7 면. 재생 속도는 Bowl.pourFps로 조절한다.
        component.shioFrames = LoadSpriteSheet(BowlDir + "Sio_Ani.png", 128, 128);
        component.shoyuFrames = LoadSpriteSheet(BowlDir + "Syo_Ani.png", 128, 128);
        component.tonkotsuFrames = LoadSpriteSheet(BowlDir + "Don_Ani.png", 128, 128);

        // 토핑을 올렸을 때 국물이 찰랑이는 16장. 한 바퀴만 돌고 붓기 시트의 면 프레임으로 돌아간다.
        component.shioToppingFrames = LoadSpriteSheet(BowlDir + "Sio_Topping.png", 128, 128);
        component.shoyuToppingFrames = LoadSpriteSheet(BowlDir + "Syo_Topping.png", 128, 128);
        component.tonkotsuToppingFrames = LoadSpriteSheet(BowlDir + "Don_Topping.png", 128, 128);

        // 드래그 중에 레이캐스트를 통과시키려면 CanvasGroup이 필요하다.
        // 없으면 그릇 자신이 SubmitZone을 가려서 제출이 영영 안 된다.
        Undo.AddComponent<CanvasGroup>(bowl.gameObject);

        // 그릇 안 재료 그림이 들어갈 자리. Bowl이 런타임에 여기로 넣는다.
        Transform contents = CreateGroup("Contents", bowl.transform);

        if (ClipUnderBroth) AttachBrothClip(contents);

        return component;
    }

    /// <summary>
    /// 국물 수면 아래에 있는 재료를 잘라내 잠긴 것처럼 보이게 한다.
    /// 국물수면.png는 수면 위만 불투명한 그림이고, Mask는 그 불투명한 자리에 있는
    /// 자식만 그린다. 그래서 재료의 잠긴 부분이 반투명이 아니라 아예 사라진다.
    ///
    /// 국물을 재료 위에 덮는 방식도 해 봤지만, 국물 층은 재료가 어디 있는지 모르기 때문에
    /// 그릇 그림의 면 무더기까지 같이 가려 버렸다. 마스크는 재료 층에만 걸리므로
    /// 잘려 나간 자리에 그릇의 국물과 면이 그대로 보인다.
    ///
    /// 잘리는 깊이는 그림이 정한다. 12픽셀로 찍혀 있고, 바꾸려면 그림을 다시 찍어야 한다.
    /// </summary>
    private static void AttachBrothClip(Transform contents)
    {
        var maskImage = Undo.AddComponent<Image>(contents.gameObject);
        maskImage.sprite = LoadSprite(BowlDir + "국물수면.png");
        maskImage.preserveAspect = true;

        // 마스크 그림이 레이캐스트를 먹으면 그릇의 OnDrop이 가려져 재료를 못 넣는다.
        maskImage.raycastTarget = false;

        var mask = Undo.AddComponent<Mask>(contents.gameObject);
        mask.showMaskGraphic = false;   // 마스크 그림 자체는 화면에 안 나온다
    }

    /// <summary>
    /// 투입이 거부됐을 때 뜨는 안내. 그릇 위쪽 절반에 걸쳐 두고 거기서부터 떠오른다.
    /// 상단바까지 올라가지 않도록 시작 높이를 낮게 잡았다.
    /// </summary>
    private static IngredientToast BuildIngredientToast(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        TextMeshProUGUI text = CreateTmpText("IngredientToast", canvas, Center, new Vector2(0f, 67f),
                                             new Vector2(233f, 27f), "", TextBody, tmpFont);
        text.color = new Color(1f, 0.45f, 0.4f);   // Bowl.RejectColor와 같은 붉은색
        text.alpha = 0f;

        var toast = Undo.AddComponent<IngredientToast>(text.gameObject);
        toast.label = text;
        return toast;
    }

    /// <summary>폐기 버튼 클릭을 Bowl.Discard에 붙인다. 인스펙터에 남는 연결이라 씬을 저장하면 유지된다.</summary>
    private static void WireDiscardButton(Image discard, Bowl bowl)
    {
        var button = Undo.AddComponent<Button>(discard.gameObject);
        button.targetGraphic = discard;
        StyleButton(button);
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
                                                     OrderResultRefs orderResult, OrderNoteRefs orderNote)
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
                                typeof(AudioSource),
                                typeof(DialogueBlip),
                                typeof(RecipeBookUI),
                                typeof(OrderNoteUI),
                                typeof(CookingHotkeys),
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
            SetPrivateReference(orderScreenUI, "dialogueViewport", orderScreen.DialogueViewport);
            SetPrivateReference(orderScreenUI, "startButton", orderScreen.Start);
            SetPrivateReference(orderScreenUI, "startButtonImage", orderScreen.StartImage);
            SetPrivateReference(orderScreenUI, "startButtonLabel", orderScreen.StartLabel);

            // 글자가 찍힐 때마다 나는 톤. 사인파 한 토막을 코드로 만들어 pitch 만 바꿔 쓴다.
            SetPrivateReference(orderScreenUI, "blip", go.GetComponent<DialogueBlip>());
        }

        // 정보 패널(? 버튼). 이것도 root를 끄는 쪽이라 패널 바깥에 붙여야 한다.
        var book = go.GetComponent<RecipeBookUI>();
        if (recipeBook != null)
        {
            SetPrivateReference(book, "root", recipeBook.Root);
            SetPrivateReference(book, "recipeNames", recipeBook.RecipeNames);
            SetPrivateReference(book, "recipeValues", recipeBook.RecipeValues);
            SetPrivateReference(book, "closeButton", recipeBook.Close);

            // 미끄러지는 것은 판 하나다. root 는 껐다 켜기만 한다.
            SetPrivateReference(book, "panel", recipeBook.Panel);
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

        // 주문 내역(Tab). 이것도 root를 끄는 쪽이라 패널 바깥에 붙여야 한다.
        var note = go.GetComponent<OrderNoteUI>();
        if (orderNote != null)
        {
            SetPrivateReference(note, "root", orderNote.Root);
            SetPrivateReference(note, "dialogueText", orderNote.Dialogue);

            // 미끄러지는 것은 종이 하나다. root 는 껐다 켜기만 한다.
            SetPrivateReference(note, "panel", orderNote.Paper);
        }

        // 단축키. Tab은 누르는 동안 주문 내역, B는 레시피 책 토글.
        var hotkeys = go.GetComponent<CookingHotkeys>();
        SetPrivateReference(hotkeys, "orderNote", note);
        SetPrivateReference(hotkeys, "recipeBook", book);
        SetPrivateReference(hotkeys, "orderManager", manager);

        // 조리 중일 때만 Tab·B가 먹게 한다. 주문 화면이나 결과창이 떠 있으면 막힌다.
        SetPrivateReference(hotkeys, "orderScreen", orderScreenUI);
        SetPrivateReference(hotkeys, "orderResult", go.GetComponent<OrderResultUI>());

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
        SetPrivateReference(gameManager, "orderNoteUI", Object.FindFirstObjectByType<OrderNoteUI>());
        SetPrivateReference(gameManager, "orderResultUI", Object.FindFirstObjectByType<OrderResultUI>());

        // ? 버튼: 주문 원문 + 기본 레시피 + 재료 속성표 (기획서 6.1).
        // 조리 화면에는 대사줄이 없으므로 이게 유일한 확인 수단이다.
        var help = Undo.AddComponent<Button>(topBar.Help.gameObject);
        help.targetGraphic = topBar.Help;
        StyleButton(help);
        UnityEventTools.AddPersistentListener(help.onClick, gameManager.ShowOrderInfo);
    }

    /// <summary>
    /// 버튼에 공통 강조색을 준다. 유니티 기본값은 차이가 거의 없어 눌러도 되는지 알기 어렵다.
    /// 색을 1보다 크게 두면 원래 색을 그만큼 밝힌다. 버튼마다 바탕색이 달라도 같은 정도로 밝아진다.
    /// </summary>
    private static void StyleButton(Button button)
    {
        // 기본 → 호버 → 누름이 한 방향으로 이어지도록 밝기를 1.0 → 0.875 → 0.75로 둔다.
        // 호버만 밝아지고 누름은 어두워지면 색이 반대로 튀어 눌린 것인지 얹은 것인지 헷갈린다.
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.875f, 0.875f, 0.875f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        // 색 변화만으로는 눌린 것이 잘 안 보인다. 판이 실제로 내려가는 반응을 같이 붙인다.
        if (button.GetComponent<ButtonPress>() == null) Undo.AddComponent<ButtonPress>(button.gameObject);
    }

    /// <summary>private [SerializeField] 칸에 값을 넣는다. 인스펙터로 꽂는 것과 같은 결과.</summary>
    /// <summary>
    /// 그림 여러 장을 배열 필드에 꽂는다. SetPrivateReference 는 한 장짜리라 배열에 못 쓴다.
    /// </summary>
    private static void SetPrivateArray(Object target, string fieldName, Object[] values)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null || !property.isArray)
        {
            Debug.LogWarning("[RamenLayoutBuilder] " + target.GetType().Name + "." + fieldName + " 배열을 찾지 못했습니다.");
            return;
        }

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

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

    private static Image CreateImage(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size,
                                     Color color, Sprite sprite = null, float pixelScale = PixelArtScale)
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

            // 9-슬라이스 모서리는 원본 픽셀 그대로 그려진다. 주변 아트가 3배라
            // 그냥 두면 판 테두리만 1픽셀로 가늘어 겉돈다. 배수를 낮춰 같은 배율로 맞춘다.
            // 판 안에 아이콘이 박힌 그림은 배수를 더 키워야 아이콘이 커진다.
            if (img.type == Image.Type.Sliced) img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.01f, pixelScale);
        }
        return img;
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
        // 굵게를 쓰지 않는다. 이유는 CreateTmpText 쪽 주석과 같다.
        text.fontStyle = FontStyle.Normal;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        // 라벨이 레이캐스트를 먹으면 슬롯의 드래그가 시작되지 않는다.
        text.raycastTarget = false;

        // 검은 테두리는 밝은 글자에만 두른다.
        // 어두운 글자에 두르면 획 사이가 메워져 흐릿하게 뭉개진다.
        // 판 위에 얹는 글자는 이미 배경과 대비가 있어 테두리가 필요 없다.
        float brightness = (color.r + color.g + color.b) / 3f;
        if (brightness > 0.5f)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = TextOutlineColor;
            outline.effectDistance = TextOutlineDistance;
        }

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
            || importer.spritePixelsPerUnit != SpritePixelsPerUnit
            || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;   // 픽셀아트라 보간하면 안 된다
            importer.spritePixelsPerUnit = SpritePixelsPerUnit;
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

    /// <summary>
    /// 배경 그림 전용. 여기만 Point 대신 Bilinear 다.
    ///
    /// 나머지 그림은 전부 원본 1픽셀 = 판 1칸이라 정수배로 커지지만, 배경은 판이 아니라
    /// 창 전체를 덮어서 1671 -> 1920 처럼 어중간한 배율이 나온다. 이걸 Point 로 늘리면
    /// 어떤 줄만 두 번 그려져 나뭇결에 굵은 줄이 생긴다. 애초에 픽셀아트가 아니라
    /// 그려진 그림이라 보간해도 잃을 격자가 없다.
    /// </summary>
    private static Sprite LoadPhotoSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 배경 그림을 찾지 못했습니다: " + path);
            return null;
        }

        if (importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || importer.filterMode != FilterMode.Bilinear
            || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>상단바·버튼에 쓰는 legacy Text용 폰트. 활성 프로필의 ttf를 쓴다.</summary>
    private static Font LoadFont()
    {
        FontProfile profile = ActiveProfile;
        EnforceRasterFontImport(profile.TtfPath);

        Font font = AssetDatabase.LoadAssetAtPath<Font>(profile.TtfPath);
        if (font != null) return font;

        Debug.LogWarning("[RamenLayoutBuilder] 폰트를 찾지 못했습니다: " + profile.TtfPath +
                         "\n내장 LegacyRuntime.ttf로 대체합니다. 한글이 네모로 보이면 맑은고딕을 이 경로에 넣어 주세요.");
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    /// <summary>
    /// ttf의 렌더링 모드를 Hinted Raster로 맞춘다.
    ///
    /// Unity 기본값은 Hinted Smooth라 legacy Text가 글자를 안티에일리어싱해서 그린다.
    /// 픽셀 폰트에 그걸 걸면 획 가장자리마다 회색 픽셀이 한 줄씩 생긴다. TMP 쪽을 래스터로
    /// 구워 놔도 상단바 글자는 legacy Text라 여기를 안 고치면 그대로 흐리다.
    /// </summary>
    private static void EnforceRasterFontImport(string ttfPath)
    {
        var importer = AssetImporter.GetAtPath(ttfPath) as TrueTypeFontImporter;
        if (importer == null) return;

        // 픽셀 폰트가 아닌 프로필은 Unity 기본값(부드럽게)이 맞다.
        FontRenderingMode wanted = ActiveProfile.Raster
            ? FontRenderingMode.HintedRaster
            : FontRenderingMode.HintedSmooth;

        if (importer.fontRenderingMode == wanted) return;

        importer.fontRenderingMode = wanted;
        importer.SaveAndReimport();
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
