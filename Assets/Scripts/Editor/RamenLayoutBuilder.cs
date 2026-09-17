using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.Video;

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
    private const int TextHead = 16;    // 갈무리7 x 2 — 소제목, 정산표 본문
    private const int TextTitle = 24;   // 갈무리11 x 2 — 팝업 제목

    private const string BowlDir = "Assets/Art/그릇/";
    private const string EtcDir = "Assets/Art/나머지/";
    private const string IngredientDir = "Assets/Art/재료/";
    private const string UiDir = "Assets/Art/UI/";
    private const string ScreenDir = "Assets/Art/화면/";
    private const string VideoDir = "Assets/Video/";

    /// <summary>타래 3통과 향미유통이 기본·선택 두 줄로 함께 들어 있는 한 장. 자르기 영역은 아래 참고.</summary>
    private const string CookDir = "Assets/Art/조리/";

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
    // 640x360 에서 960x540 으로 올렸다. 새 조리 그림이 128칸이라 640 판에서는
    // 타래 3통만으로 가로의 42%를 먹어 그릇이 한가운데에 못 선다.
    // 960 이면 배율이 1920x1080 에서 2배로 여전히 정수라 픽셀이 안 깨지고,
    // 타래 3통이 28% 로 배치도와 거의 같아진다.
    // 1920 까지 올리면 배율이 1배가 되어 128칸 그림이 화면에서 128픽셀 — 너무 작다.
    private static readonly Vector2 DesignResolution = new Vector2(960f, 540f);

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
    private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

    // 원본 픽셀의 몇 배로 띄울지를 실제 물건 크기 느낌에 맞춰 정한 것.
    // 냄비 > 재료통·타래통 > 조미료병 순서고, 면 튀김기가 제일 큰 장비다.
    // 정산 팝업은 밝은 판 위에 글자를 얹으므로 조리 화면의 흰 글자를 그대로 쓸 수 없다.
    private static readonly Color PopupInkColor = new Color(0.16f, 0.16f, 0.16f);

    /// <summary>어두운 판 위에 얹는 글자색. 순백은 픽셀 그림 위에서 너무 튄다.</summary>
    private static readonly Color DarkPanelInkColor = new Color32(242, 230, 208, 255);

    /// <summary>금테 두른 판 위의 글자색. 테와 같은 계열이라 판이 한 덩어리로 읽힌다.</summary>
    private static readonly Color GoldInkColor = new Color32(238, 206, 140, 255);

    // 주문 결과창 글자색 넷. 네 덩어리가 다 같은 회색이면 어디를 봐야 할지 알 수 없어서
    // 역할대로 갈랐다. 크기도 같이 갈린다 — 24 / 24 / 16 / 12 순이다.
    //
    // 정확도와 보상이 둘 다 24인 것은 일부러다. 정확도를 30으로 올려 봤더니 명패 안쪽
    // 평평한 칸이 29 라(위 밝은 띠 7 + 아래 어두운 띠 8) 글자가 경사면을 밟아 탁해졌다.
    // 정확도는 명패가 감싸 주므로 크기가 같아도 보상과 안 헷갈린다.
    //
    //   정확도  판 테두리와 같은 먹갈색. 종이에 박힌 잉크로 읽힌다
    //   한마디  한 단 옅게. 정보가 아니라 「말」이라서
    //   보상    도장(대박·명인)과 같은 붉은 먹. 아트 가족이 이어진다
    //   누적    제일 옅게. 곁다리 정보다
    private static readonly Color ResultInkColor = new Color32(58, 26, 12, 255);
    private static readonly Color ResultQuoteColor = new Color32(90, 62, 43, 255);
    private static readonly Color ResultRewardColor = new Color32(163, 58, 24, 255);
    private static readonly Color ResultFaintColor = new Color32(125, 106, 86, 255);

    // 주문서 색. 영수증 종이라 상단바 판(흰색)보다 살짝 누렇다.
    private static readonly Color NotePaperColor = new Color32(250, 244, 227, 255);

    /// <summary>비법서 글 색. 수첩 그림(Tools/make_recipe_book.py)의 먹색과 같다.</summary>
    private static readonly Color RecipeInkColor = new Color32(60, 42, 30, 255);
    private static readonly Color NoteShadeColor = new Color32(206, 190, 158, 255);
    private static readonly Color NoteOutlineColor = new Color32(0, 0, 0, 255);

    /// <summary>주문서에서 본문보다 흐리게 두는 것들. 구분선과 머리 정보줄이 쓴다.</summary>
    private static readonly Color NoteFadeColor = new Color32(122, 110, 88, 255);

    /// <summary>
    /// 그레인 노이즈 그림의 한 변.
    ///
    /// 칸 하나가 텍셀 하나라 이 값이 곧 "무늬가 몇 칸마다 반복되는가"다. 64 면 화면 가로
    /// 640 칸에 열 번 반복된다. 매 프레임 통째로 밀기 때문에 반복이 눈에 띄지 않는다.
    /// </summary>
    private const int GrainNoiseSize = 64;

    // 크기는 전부 원본 PNG의 픽셀 수 그대로다. 기준 격자 1칸 = 원본 1픽셀이므로
    // 배율을 곱할 자리가 없다. 여기에 1.5배 같은 값이 끼면 그 순간 픽셀이 깨진다.
    private static readonly Vector2 BinSize = new Vector2(64f, 64f);           // 재료통·타래통·조미료병 원본 64px
    private static readonly Vector2 PotSize = new Vector2(64f, 64f);           // 육수 냄비 원본 64px
    // 면 튀김기 원본 84px. 예전에는 128 그림을 1920 판에서 2배(256)로 놓았는데, 다른 그림은
    // 전부 3배였다. 즉 튀김기만 일부러 2/3로 줄여 쓰고 있었다. 1:1 판으로 옮기면서 그 축소가
    // 사라져 튀김기만 1.5배 커졌다. 그래서 128을 2/3인 85로 다시 찍되, 홀수면 두 대를 붙일 때
    // 가장자리가 반칸에 걸리므로 짝수인 84로 맞췄다.
    private static readonly Vector2 NoodleBinSize = new Vector2(84f, 84f);

    /// <summary>
    /// 아직 안 들어온 재료통에 얹는 자물쇠 크기(Tools/make_lock_icon.py, 48x48).
    /// 그림 크기 그대로 쓴다 — 늘리면 정수배가 아니어서 획이 뭉개진다.
    ///
    /// 24 로 구웠더니 김(진한 초록)·목이(진한 갈색)처럼 어두운 통 위에서 묻혔다.
    /// 48 이면 재료통(120x88) 높이의 절반을 넘어 한눈에 읽힌다.
    /// </summary>
    private const float LockIconSize = 48f;

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

    /// <summary>
    /// 그릇 김 한 칸(Tools/make_bowl_steam.py, 8칸 시트)의 <b>원본</b> 크기.
    /// 시트를 자르는 데 쓰는 값이라 그림을 다시 굽지 않는 한 건드리지 않는다.
    /// </summary>
    private static readonly Vector2 BowlSteamFrame = new Vector2(128f, 96f);

    /// <summary>
    /// 화면에 그리는 김 크기. **그릇과 같은 배율을 먹어야 한다.**
    ///
    /// 2026-09-16 까지 원본 크기를 그대로 썼다. 그릇은 <see cref="BowlScale"/> 배로 커져 있는데
    /// 김만 안 커져서, 그릇 폭 240 짜리 위에 84 짜리 가는 연기가 떴다(그림에서 잰 값 —
    /// 빈그릇.png 는 128 칸 중 120, 김 한 칸은 128 중 84 를 쓴다). 위 주석에 처음부터
    /// "폭은 그릇과 같고" 라고 적혀 있었는데 코드가 안 따라간 것이다.
    /// </summary>
    private static readonly Vector2 BowlSteamSize = BowlSteamFrame * BowlScale;

    /// <summary>
    /// 김 판을 그릇 윗변에서 얼마나 더 올릴지. **그릇 배율을 같이 먹는다.**
    ///
    /// 세 자리를 다 찍어 보고 골랐다.
    ///   수면에 맞춤    김이 재료를 덮어 라멘이 뿌예 보인다. 피어오르는 김이 아니라 안개다
    ///   <b>테두리 위</b>  아랫단이 그릇 먼 테두리에 걸치고 나머지가 나무 배경 위로 오른다
    ///   더 위로        그릇과 떨어져 혼자 뜬 연기가 된다
    ///
    /// 배율을 안 먹이면 「더 위로」가 된다. 그림에서 재 보면 그릇 테두리는 그릇 한가운데서
    /// +80 이고, 김 그림 아랫단은 판 한가운데에서 -94 다. 판을 128+38 에 두면 아랫단이 +72 —
    /// 테두리보다 8 아래라 걸터앉는다. 배율을 뺀 예전 값(판 128x96 을 128+19 에)에서는
    /// 아랫단이 +100 이라 테두리 위로 20 이 떠서, 그릇과 떨어진 연기로 보였다.
    /// </summary>
    private const float BowlSteamRise = 19f * BowlScale;

    /// <summary>김이 넘어가는 속도. 손님 그릇(6)보다 느리게 둬서 더 은은하다.</summary>
    private const float BowlSteamFps = 5f;

    /// <summary>우주 배경 한 변. 화면 대각선(√(960²+540²) ≈ 1101)보다 커야 돌려도 구석이 안 비친다.</summary>
    private static readonly Vector2 CosmosSize = new Vector2(1104f, 1104f);

    /// <summary>
    /// 손님 그림 한 변. Tools/bake_customers.py 가 굽는 크기와 같아야 한다.
    /// 원본은 397 인데 화면에서 그만큼 크면 노렌 창을 넘어선다.
    /// </summary>
    private const float CustomerPortraitSize = 300f;

    /// <summary>
    /// 손님 앞에 놓는 그릇. 손님이 300 이라 120 으로는 장난감처럼 작아 보였다.
    /// 조리 화면 중앙 그릇(256)보다는 작게 두어 그것과 헷갈리지 않게 한다.
    /// </summary>
    private const float CustomerBowlSize = 220f;

    /// <summary>
    /// 그 그릇의 높이.
    ///
    /// -75 였다. 그러면 그릇이 바닥에 닿는 자리(그림 220칸 중 위에서 185번째 줄)가 화면 y
    /// -150 이라, 카운터 맨 위 판이 아니라 그 아래 어두운 판에 놓인 꼴이었다.
    ///
    /// 카운터 그림(960x198)을 실제로 재 보니 맨 위 판은 그림 y 4~54 이고, 그 판은 화면에서
    /// -76 ~ -126 에 놓인다(카운터 윗변이 -72 이고 그림 한 칸이 화면 한 칸이다).
    /// 그 판 한가운데인 -101 에 그릇 바닥을 맞추면  -101 + 75 = -26  이다.
    /// </summary>
    private const float CustomerBowlY = -26f;

    /// <summary>
    /// 그릇이 카운터에 닿는 그림자의 피벗 높이(0이면 칸 아래끝, 1이면 위끝).
    ///
    /// 그림자는 그릇과 같은 220칸에 같은 자리로 구워 두어서(Tools/make_eating_props.py),
    /// 그릇과 겹쳐 놓기만 하면 발밑에 맞는다. 대신 타원 한가운데가 칸 위에서 185번째 줄이라
    /// 칸 한가운데가 아니다.
    ///
    /// 국물을 마실 때 그림자가 **제자리에서** 좁아져야 한다. 피벗을 칸 한가운데(0.5)에 두면
    /// 줄일 때 타원이 위로 딸려 올라가, 카운터에 남는 게 아니라 그릇을 따라 뜬 꼴이 된다.
    /// </summary>
    private const float BowlShadowPivotY = 1f - 185f / CustomerBowlSize;

    /// <summary>
    /// 땀방울 크기와 자리(손님 자리 안에서의 상대값).
    ///
    /// 원본이 32칸이라 그대로 쓴다. 16칸으로 그렸을 때는 손님이 300칸이라 화면에서 거의 안 보였다.
    /// 자리는 얼굴 오른쪽 바깥, 눈썹 높이다 —
    /// 얼굴 안에 얹으면 뺨에 붙은 점으로 보이고, 너무 벌리면 허공에 뜬다.
    /// </summary>
    private static readonly Vector2 SweatSize = new Vector2(32f, 32f);
    private static readonly Vector2 SweatPos = new Vector2(88f, 10f);

    /// <summary>
    /// 까마귀 한 칸. Tools/make_crow.py 가 굽는 48칸 4프레임 시트를 그대로 쓴다.
    /// 0 다묾 · 1 반쯤 · 2 까악 · 3 반쯤 순서다.
    /// </summary>
    private static readonly Vector2 CrowSize = new Vector2(48f, 48f);

    /// <summary>
    /// 침묵의 점 셋. 손님 머리 위에 가로로 늘어선다.
    ///
    /// 222 에 두었더니 상단바(아래변이 210 근처)와 겹쳤다. 시네마틱 동안에는 상단바가 꺼지지만
    /// 그 밖에서도 쓸 수 있어야 하므로 판 아래로 내린다.
    ///
    /// 손님 그림 아래변이 카운터 선(−72)에서 14칸 더 내려간 −86 이라, 머리 꼭대기는
    /// 제일 큰 손님(사극, 291칸)이 205, 보통은 190 안팎이다. 188 이면 머리 바로 위에 얹힌다.
    ///
    /// 간격은 넓게 둔다. 까마귀가 점 사이를 건너는 시간이 곧 점이 찍히는 간격이라,
    /// 좁으면 세 점이 한꺼번에 툭 떠 버린다.
    /// </summary>
    private static readonly Vector2 DotSize = new Vector2(28f, 28f);
    private const float DotY = 188f;
    private const float DotGap = 90f;

    // 조미료 배지(시치미·향미유). 그릇 오른쪽 위 빈 나무판에 두 줄로 쌓는다.
    // 그릇 오른쪽 끝과 시치미 병 사이가 비어 있어 거기 들어간다.
    // 모두 캔버스 한가운데 기준 상대값이다. 화면이 커져도 같이 따라간다.
    // 아이콘은 원본이 24x24다. 16으로 그렸더니 옆 글자보다 작아 눈에 안 들어왔다.
    // 늘려 쓰지 않고 그 크기로 다시 그렸다 — 정수배가 아니면 획이 반칸에 걸린다.
    private static readonly Vector2 BadgeIconSize = new Vector2(24f, 24f);
    private const float BadgeX = 150f;
    private const float BadgeTopY = 118f;
    private const float BadgeRowGap = 34f;
    private const float BadgeCountX = 42f;   // 아이콘 중심에서 글자 상자 중심까지

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

    /// <summary>
    /// 말풍선 테두리와 글자 사이 여백.
    ///
    /// 눈에 보이는 여백은 이 값과 조금 다르다. TMP 가 글자 좌우에 제 여백을 더 두어서,
    /// 가로는 설정값보다 2~3칸 더 벌어져 보인다. 재 보니 가로 6 · 세로 10 일 때
    /// 실제로는 왼쪽 8.5 · 위 10.5 였다.
    ///
    /// 그래서 사방이 고르게 보이도록 가로를 세로보다 작게 잡는다.
    /// 가로는 10 이었다. 상자가 화면 왼쪽으로 삐져나가 글자가 잘려서 2 줄였다
    /// (<see cref="BubbleMaxTextWidth"/> 참고). TMP 제 여백까지 더하면 여전히 10 남짓 보인다.
    /// </summary>
    private const float BubblePaddingX = 8f;
    private const float BubblePaddingY = 12f;

    /// <summary>
    /// 말풍선 글자가 쓸 수 있는 최대 폭.
    ///
    /// 말풍선은 손님 얼굴 왼쪽에 붙고 높이는 그 손님 머리에 맞춘다(OrderScreenUI.PlaceBubble).
    /// 오른쪽 끝은 OrderScreenUI.BubbleRightX 가 정하고 지금 -130 이다. 화면 왼쪽 끝(-480)까지
    /// 350 이 남으므로, 좌우 여백 <see cref="BubblePaddingX"/> 8 씩을 빼면 334 까지 쓸 수 있다.
    ///
    /// 340 이었다. 여백까지 더하면 상자가 360 이라 화면 왼쪽으로 10 칸 삐져나가 글자가 잘렸다
    /// (오른쪽 끝을 -100 으로 잘못 적어 둔 주석을 보고 계산한 값이었다).
    /// 지금 값은 가장자리에서 8 칸 떨어진다.
    /// </summary>
    private const float BubbleMaxTextWidth = 326f;

    /// <summary>
    /// 말풍선이 가장 커졌을 때의 높이. 여백(위아래 12) + 가장 긴 마디(39)다.
    ///
    /// 39 는 재서 나온 값이다. 마디 16464 개를 글자폭 340 으로 재 보니 91% 가 한 줄,
    /// 나머지가 두 줄이고 세 줄은 하나도 없었다. 가장 높은 것이 39 칸이다.
    /// 예전에는 "네 줄 80칸"으로 잡혀 있었는데 그런 마디는 나오지 않는다.
    ///
    /// 넵 버튼 자리를 이 값에 맞춘다. 버튼은 늘 "상자가 가장 커졌을 때 아래변"에 서 있는다.
    /// 그래야 마디마다 상자 높이가 달라져도 버튼이 위아래로 안 뛴다.
    ///
    /// 화면에 실제로 보이는 높이는 이 값이 아니다. OrderScreenUI 가 글에 맞춰 상자를 다시 잡으므로
    /// 여기 값은 첫 줄이 찍히기 전까지의 초기 크기일 뿐이다. 여기만 고쳐도 화면은 안 바뀐다.
    /// 여백을 바꾸려면 BubblePaddingX / BubblePaddingY 를 고친다.
    /// </summary>
    private const float BubbleHeight = 39f + BubblePaddingY * 2f;

    /// <summary>말풍선 아래변과 넵 버튼 사이 간격.</summary>
    private const float BubbleButtonGap = 4f;

    // ── 주문서 치수 ───────────────────────────────────────────────
    //
    // 종이 길이는 여기서 정하지 않는다. 대사가 길면 길어지고 짧으면 짧아진다.
    // 가장 긴 주문에 맞춰 고정하면 흔한 아홉 줄짜리에서 아래가 절반 넘게 빈다.
    // 주문 4000건을 재 보니 7~12줄이 대부분이고 13줄 이상은 2%, 17줄은 4000건에 셋뿐이었다.
    //
    // 폭은 좁을수록 세로로 길어진다. 줄이 접히기 때문이다.
    // 본문 10 · 제목 12 로 12000건을 재 보면 가장 긴 주문이 이렇게 나온다.
    //   176 -> 267칸 · 184 -> 267칸 · 192 -> 252칸 · 200 -> 237칸
    //
    // 184 는 영수증답게 세로로 서면서(보통 주문에서 세로/가로가 1.5 언저리) 가장 긴 주문도
    // 화면 안에 들어가는 값이다. 높이는 대사에 맞춰 OrderNoteUI 가 매번 다시 잡는다.

    /// <summary>주문서 글자가 쓸 수 있는 폭.</summary>
    private const float NoteTextWidth = 156f;

    /// <summary>종이 테두리와 글자 사이 여백. 사방 같은 값을 쓴다.</summary>
    private const float NoteMargin = 14f;

    /// <summary>종이 폭. 글자폭 + 여백 양쪽.</summary>
    private const float NoteWidth = NoteTextWidth + NoteMargin * 2f;

    /// <summary>
    /// 주문서 본문 글자 크기.
    ///
    /// 손님 대사창이 15 라 주문서는 그보다 작아야 한다. 주문서는 대사를 옮겨 적은 것이라
    /// 대사창보다 크면 주객이 바뀐다. 12 로 해 봤더니 종이가 348 까지 커져 조리 화면을
    /// 너무 가렸다. 쓸 수 있는 크기는 8·10·12·15 의 배수뿐이라 한 단계 아래가 10 이다.
    /// </summary>
    private const int NoteFontSize = TextSmall;

    /// <summary>
    /// 주문서 본문 줄 간격.
    /// 글자(10)에 딱 붙이면 줄이 엉겨 읽기 나쁘다. 두 칸 띄운다.
    /// </summary>
    private const float NoteLineHeight = 12f;

    /// <summary>아래 톱니 높이. 이 안에서 찢긴 모양이 오르내린다.</summary>
    private const float NoteTornHeight = 6f;

    /// <summary>
    /// 마우스를 올렸을 때 주문서가 남기는 진하기.
    ///
    /// 종이가 그릇과 재료통을 덮고 있어서, 보면서 조리하려면 뒤가 비쳐야 한다.
    /// 0.4 까지 내리면 뒤는 잘 보이지만 정작 주문 글자가 안 읽힌다.
    /// </summary>
    private const float NoteHoverAlpha = 0.7f;

    /// <summary>제목 '주문서' 크기. 본문 바로 윗단계다.</summary>
    private const int NoteTitleFontSize = TextBody;

    /// <summary>
    /// 종이에서 글자를 뺀 나머지 높이. 머리글·구분선·정보줄을 다 더한 값이다.
    ///
    /// 위 61 = 여백 8 + 머리글 14 + 6 + 선 1 + 5 + 정보줄 14 + 5 + 선 1 + 7
    /// 아래 12 = 7 + 선 1 + 4
    ///
    /// 톱니는 여기 안 들어간다. 종이 아래에 매달려 있어서 종이 높이 밖이다.
    /// 눈에 보이는 전체 길이는 이 값 + 글자 + 톱니 6 이다.
    ///
    /// OrderNoteUI 가 종이 높이를 이 값 + 글자 높이로 잡는다. 배치를 고치면 여기도 고칠 것.
    /// </summary>
    private const float NoteChromeHeight = 61f + 12f;

    // ── 결과창 세 판 ──────────────────────────────────────────────
    //
    // 왼쪽부터 **내가 만든 그릇 · 주문서 · 정확도 판**이 나란히 선다.
    // 점수만 띄우면 무엇을 틀렸는지 알 길이 없어서, 낸 그릇과 받은 주문을 같이 펼쳐 둔다.
    //
    //   256  +16+  184  +32+  300  = 788   (화면 960, 양옆 86칸씩 남는다)
    //   그릇       주문서       정확도

    private const float ResultPanelWidth = 300f;
    private const float ResultPanelHeight = 330f;

    /// <summary>
    /// 결과창에 얹는 그릇. 조리대 그릇(BowlSize)과 **같은 크기**다.
    /// 기획 그림의 220 으로 줄이면 0.86배라 획이 반픽셀에 걸려 가장자리에 회색이 낀다.
    /// </summary>
    private const float ResultBowlSize = 128f * BowlScale;

    /// <summary>그릇과 주문서 사이. 주문서와 정확도 판 사이는 더 벌린다 — 왼쪽 둘은
    /// "내가 낸 것과 받은 주문"으로 한 묶음이고, 점수판은 그 결과라 한 칸 떨어져야 한다.</summary>
    private const float ResultBowlGap = 16f;
    private const float ResultNoteGap = 32f;

    /// <summary>세 판을 늘어놓은 전체 폭. 가운데 정렬이라 자리는 여기서 나온다.</summary>
    private const float ResultRowWidth =
        ResultBowlSize + ResultBowlGap + NoteWidth + ResultNoteGap + ResultPanelWidth;

    private const float ResultBowlX = -ResultRowWidth * 0.5f + ResultBowlSize * 0.5f;
    private const float ResultNoteX =
        -ResultRowWidth * 0.5f + ResultBowlSize + ResultBowlGap + NoteWidth * 0.5f;
    private const float ResultPanelX = ResultRowWidth * 0.5f - ResultPanelWidth * 0.5f;

    /// <summary>
    /// 말풍선 꼬리를 원본 실루엣의 몇 배로 키울지. 정수배만 쓴다.
    ///
    /// 원본 8x5 는 240x96 짜리 말풍선에 달기엔 눈에 안 띈다. 그렇다고 그림을 통째로 늘리면
    /// 1픽셀 외곽선까지 같이 굵어져 본문 테두리와 따로 논다. 실루엣만 키우고 외곽선은
    /// 다시 1픽셀로 긋는다(BuildBubbleTailBytes).
    ///
    /// 3 이면 꼬리 오른쪽 끝이 화면 x -56 이다. 손님 슬롯(-63)에는 들어가지만 그쪽은 여백이라
    /// 손님 그림(-47)까지 9칸 남는다. 더 키우려면 그 두 값을 먼저 확인할 것.
    /// </summary>
    private const int BubbleTailScale = 3;

    /// <summary>
    /// 말풍선 위에서 꼬리 윗변까지. 정수라야 반칸에 안 걸린다.
    ///
    /// 상자 높이는 말 길이에 따라 변하는데 윗변은 고정이라, 이 거리도 고정이면 꼬리가
    /// 늘 같은 자리에 선다. 다만 가장 작은 상자(한 줄 = 43칸)에도 꼬리(15칸)가 다
    /// 들어가야 한다. 37 로 잡았더니 한 줄짜리에서 꼬리가 상자 아래로 9칸 삐져나와,
    /// 상자와 꼬리가 따로 노는 것처럼 보였다.
    ///   24 + 15 = 39  <=  43   (아래로 4칸 남는다)
    /// </summary>
    private const float BubbleTailTop = 24f;

    /// <summary>말풍선 윗변의 화면 y. 꼬리 한가운데가 손님 귀(y 98)에 서도록 잡은 값이다.</summary>
    private const float BubbleTopY = 130f;

    // 말풍선이 Order UI.png 안에서 차지하는 자리. 왼쪽 아래가 (0,0)이다.
    private const int BubbleBodyX = 7;
    private const int BubbleBodyY = 19;
    private const int BubbleBodyW = 46;
    private const int BubbleBodyH = 28;

    // 꼬리. 본문 테두리 칸(x 52)까지 넣어야 입이 어디서 열리는지가 실루엣에 남는다.
    private const int BubbleTailX = 52;
    private const int BubbleTailY = 34;
    private const int BubbleTailW = 8;
    private const int BubbleTailH = 5;
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
    // 시치미 병은 그림이 24x56 이라 2배로 놓으면 48x112 가 된다.
    // 재료통 왼쪽 열 위쪽 빈자리(y 66~334)에 넉넉히 들어간다.
    private static readonly Vector2 ChiliBottleSize = new Vector2(128f, 128f);

    // 잘라 낸 통(60x44)을 2배로 놓는다. 정확히 2배라 픽셀이 안 깨진다.
    // 3배(180x132)면 네 줄이 세로 528이 되어 상단바 아래 500에 안 들어간다.
    private static readonly Vector2 IngredientBinSize = new Vector2(120f, 88f);

    /// <summary>
    /// 끌고 다니는 그림의 기본 한 변. IngredientSlot 이 커서 크기(64)에 0.5를 곱해 쓰는 값과 같다.
    /// 그림 크기를 이 값으로 나눠 배율을 정한다.
    /// </summary>
    private const float GhostBaseSide = 32f;

    // 새 타래통은 128칸 그림이고 그 안에서 통이 87~93 x 99~104 를 차지한다.
    // 칸째로 놓고 preserveAspect 에 맡긴다.
    private static readonly Vector2 TareBinSize = new Vector2(128f, 128f);
    // 향미유통만 칸이 91x100 이다. 다른 것과 달리 128 로 패딩돼 있지 않다.
    private static readonly Vector2 OilBinSize = new Vector2(91f, 100f);

    /// <summary>
    /// 눕혀 쓰는 이름표. 눕히면 화면에서 16 x 46으로 보인다.
    /// 46을 넘기면 맨 아래 재료통 이름표가 화면 밖으로 나간다. 통이 44고 마지막 통이
    /// 화면 밑변에 붙어 있어서, 통보다 긴 이름표는 갈 데가 없다.
    /// </summary>
    private static readonly Vector2 RotatedLabelBoxSize = new Vector2(46f, 16f);

    /// <summary>재료통 이름표를 통 오른쪽으로 밀어내는 거리. 통 30 + 간격 2 + 눕힌 이름표 8.</summary>
    private const float BinLabelOffsetX = 40f;

    /// <summary>육수 냄비. 원본 64를 2배로 놓는다. 와이어프레임에서 가장 큰 통이다.</summary>
    // 128칸 그림을 2배로 놓는다. 향미유통(폭 79)의 세 배쯤 되어 와이어프레임과 비슷해진다.
    // 정확히 2배라 픽셀이 안 깨진다. 어중간한 배율은 가장자리가 반칸에 걸린다.
    private static readonly Vector2 BrothPotSize = new Vector2(256f, 256f);

    /// <summary>타래 이름표를 통 오른쪽으로 밀어내는 거리. 통 32 + 간격 2 + 이름표 32.</summary>
    private const float TareLabelOffsetX = 66f;

    private const float PanelScale = 1f;
    /// <summary>
    /// 상단 판 높이.
    ///
    /// 판이 960x540 으로 넓어지면서 18 짜리 판이 화면에 비해 너무 작아졌다. 두 배로 키웠다.
    /// 판 그림은 9-슬라이스라 아무리 키워도 테두리는 1픽셀로 남는다. 글자만 한 단계 올린다.
    /// </summary>
    private const float PanelBarHeight = 36f;

    /// <summary>상단 바에서 만들어 두고 나중에 다른 것과 연결해야 하는 것들.</summary>
    private class TopBarRefs
    {
        public Transform Root;     // 시네마틱 동안 통째로 꺼진다
        public Image Discard;      // 그릇이 생긴 뒤 onClick을 붙인다
        public Image Submit;       // 마무리 버튼. 마찬가지로 나중에 붙인다
        public TextMeshProUGUI DayText;       // GameManager가 일차를 써 넣는다
        public DayClockIcon DayClock;         // GameManager가 지나간 손님 수만큼 조각을 채운다
        public TextMeshProUGUI RevenueText;   // GameManager가 누적 매출을 써 넣는다
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

        /// <summary>대사 왼쪽 손님 초상. 그림은 실행 중에 손님마다 갈린다.</summary>
        public Image CustomerFace;
        public Button Confirm;

        /// <summary>판 왼쪽, 내가 만든 그릇. 그림은 제출하는 순간에 복제해 온다.</summary>
        public Image ServedBowl;

        /// <summary>판 가운데, 붙박이 주문서. Tab 으로 여는 것과 종이만 같다.</summary>
        public OrderNoteRefs Note;
    }

    /// <summary>주문 내역(Tab)에서 OrderNoteUI에 꽂아 줘야 하는 것들.</summary>
    private class OrderNoteRefs
    {
        public GameObject Root;
        public TextMeshProUGUI Dialogue;

        /// <summary>미끄러져 들어오는 종이. OrderNoteUI 가 이것만 움직인다.</summary>
        public RectTransform Paper;

        /// <summary>종이 전체를 한꺼번에 흐리게 하는 데 쓴다.</summary>
        public CanvasGroup Fade;

        /// <summary>영수증 머리의 정보줄. 왼쪽은 며칠째, 오른쪽은 몇 번째 손님.</summary>
        public TextMeshProUGUI DayLabel;
        public TextMeshProUGUI CustomerLabel;
    }

    /// <summary>레시피 책(B 키)에서 RecipeBookUI에 꽂아 줘야 하는 것들.</summary>
    private class RecipeBookRefs
    {
        public GameObject Root;
        public TextMeshProUGUI[] MenuNameTexts;
        public TextMeshProUGUI[] MenuValueTexts;
        public Button Close;

        /// <summary>왼쪽에서 나오는 판.</summary>
        public RectTransform Panel;

        /// <summary>판에 씌운 CanvasGroup. 이제 흐려지지 않고 알파를 1 로 굳히는 데만 쓴다.</summary>
        public CanvasGroup Fade;

        /// <summary>마우스를 올린 재료 위에 뜨는 이름. 글자와 검은 복제본이 한 묶음이다.</summary>
        public RectTransform HoverName;

        /// <summary>그 묶음 안의 흰 글자.</summary>
        public TextMeshProUGUI HoverLabel;
    }

    /// <summary>주문 화면에서 OrderScreenUI에 꽂아 줘야 하는 것들.</summary>
    private class OrderScreenRefs
    {
        public GameObject Root;
        public TextMeshProUGUI DayTime;
        public DayClockIcon DayClock;
        public TextMeshProUGUI Revenue;
        public TextMeshProUGUI Dialogue;
        public RectTransform DialogueViewport;

        /// <summary>밤 배경. 화면이 밀려 올라갈 때 같이 걷힌다.</summary>
        public Image Backdrop;

        public Button Start;
        public Image StartImage;
        public TextMeshProUGUI StartLabel;

        /// <summary>손님 겉모습. 새 손님을 맞을 때만 OrderScreenUI 가 다시 뽑는다.</summary>
        public CustomerAppearance Look;

        /// <summary>먹는 네 컷 연출. OrderScreenUI 는 나중에 꽂아 준다.</summary>
        public EatingCutscene Cutscene;
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
        public TextMeshProUGUI TargetProfit; // 오늘 목표액
        public TextMeshProUGUI Profit;
        public TextMeshProUGUI TotalProfit;  // 누적 매출
        public TextMeshProUGUI Accuracy;
        public TextMeshProUGUI Perfect;
        public Button Confirm;
        public Button Retry;                 // 목표 미달이면 이쪽이 뜬다
    }

    /// <summary>정산 팝업 프리팹. 모양은 B가 에디터에서 쥔다.</summary>
    private const string ResultPopupPrefab = "Assets/Prefabs/UI/TodayReciept.prefab";

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

        /// <summary>
        /// 0보다 크면 통 그림을 이 칸으로 잘라 SpriteLoop 가 돌린다.
        /// 끓는 육수 냄비처럼 가만히 있어도 움직여야 하는 통에 쓴다.
        /// </summary>
        public readonly int LoopCell;

        public SlotDef(IngredientType type, string label, Vector2 anchor, float x, float y, Vector2 size,
                       string binPath, string bowlPath = null, string dragPath = null, bool liquid = false,
                       float labelX = 0f, float labelY = 0f, string idSuffix = null, bool labelAbove = false,
                       Rect crop = default, bool labelRotated = false, Rect hoverCrop = default, int loopCell = 0)
        {
            LoopCell = loopCell;
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
        // 통이 128칸으로 커져서 세로로 못 세운다. 셋을 가로로 눕혔다(배치도와 같다).
        // 칸은 128이지만 안의 통은 90 안팎이라, 간격 110 이면 칸끼리는 겹쳐도 통끼리는 20 뜬다.
        new SlotDef(IngredientType.ShioTare,     "시오",     TopLeft, 70f, -100f, TareBinSize,
                    CookDir + "타래_시오.png", liquid: true),
        new SlotDef(IngredientType.ShoyuTare,    "쇼유",     TopLeft, 175f, -100f, TareBinSize,
                    CookDir + "타래_쇼유.png", liquid: true),
        new SlotDef(IngredientType.TonkotsuBase, "돈코츠",   TopLeft, 280f, -100f, TareBinSize,
                    CookDir + "타래_돈코츠.png", liquid: true),

        // 향미유. 가운데 타래(쇼유) 바로 아래에 x 를 맞춰 세운다.
        // 예전에는 화면 오른쪽 시치미 위에 있었는데, 액체는 왼쪽에 모으는 편이 손이 덜 간다.
        new SlotDef(IngredientType.FlavorOil,    "향미유",   TopLeft, 175f, -215f, OilBinSize,
                    CookDir + "향미유통.png", liquid: true),

        // 육수 냄비. 원본 64를 2배로 놓아 왼쪽 아래를 채운다(화면 8~136 x 232~360).
        // 이름표는 와이어프레임처럼 냄비 안에 얹는다. 아래에 두면 화면 밖으로 나간다.
        // 육수 냄비. 4프레임 시트라 첫 칸만 통 그림으로 쓰고, 나머지는 SpriteLoop 가 돌린다.
        new SlotDef(IngredientType.Broth,        "육수",     TopLeft, 140f, -400f, BrothPotSize,
                    CookDir + "육수 냄비.png", liquid: true, loopCell: 128),

        // 면은 재료통이 아니라 면통(BuildNoodlePot)이 맡는다. 바구니 둘을 한 그림이 덮고 있어서
        // 슬롯 표로는 못 만든다.

        // 조미료 2종. 튀김기와 재료통 사이에 세로로 둘을 세운다.
        // 와이어프레임처럼 이름표를 눕혀 병 왼쪽에 붙인다. 병이 좁아 아래에 두면 줄이 어긋난다.
        // 클릭하면 젓가락 대신 병 자체를 들고, 그릇에 대면 기울여 뿌린다.
        // 그릇용 그림이 없어 수량만 세고 그릇에는 안 나온다.
        // 시치미. 재료통 왼쪽 열의 빈 위쪽(x 711~831, y 66~334) 한가운데에 세운다.
        // 조미료라 재료통 무리 안에 두는 편이 손이 덜 간다.
        new SlotDef(IngredientType.ChiliPowder,  "시치미",   MidRight, -189f, 70f, ChiliBottleSize,
                    EtcDir + "시치미.png", liquid: true),

        // 오른쪽: 재료통 7개를 두 열로. 배치도가 두 열이다.
        //   오른쪽 열(x -65)   멘마 · 차슈 · 파 · 김 · 계란   다섯 칸을 딱 붙여 세운다
        //   왼쪽 열 (x -189)  숙주 · 목이버섯                 아래 두 칸에만 붙인다
        //
        // 통이 88 이라 줄 간격 90 이면 2칸만 뜬다. 열 사이도 4칸이다.
        // 다섯 칸이 y 66~514 를 채우고, 왼쪽 둘이 그 아래쪽에 붙어 배치도의 ㄴ 자 모양이 된다.
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
        new SlotDef(IngredientType.Menma,        "멘마",     MidRight, -65f, 160f, IngredientBinSize,
                    IngredientDir + "멘마 재료통.png", IngredientDir + "멘마 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
        new SlotDef(IngredientType.Chashu,       "차슈",     MidRight, -65f, 70f, IngredientBinSize,
                    IngredientDir + "차슈 재료통.png", IngredientDir + "차슈 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
        new SlotDef(IngredientType.GreenOnion,   "파",       MidRight, -65f, -20f, IngredientBinSize,
                    IngredientDir + "파 재료통.png", IngredientDir + "파 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
        new SlotDef(IngredientType.Nori,         "김",       MidRight, -65f, -110f, IngredientBinSize,
                    IngredientDir + "김 재료통.png", IngredientDir + "김 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
        new SlotDef(IngredientType.Egg,          "계란",     MidRight, -65f, -200f, IngredientBinSize,
                    IngredientDir + "계란 재료통.png", IngredientDir + "계란 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
        // 숙주만 파일 이름이 "테두리 강조"가 아니라 "테두리"다.
        new SlotDef(IngredientType.BeanSprout,   "숙주",     MidRight, -189f, -110f, IngredientBinSize,
                    IngredientDir + "숙주 재료통.png", IngredientDir + "숙주 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
        // 눕힌 이름표가 46칸뿐이라 네 글자는 안 들어간다. 와이어프레임도 "목이"로 줄여 적혀 있다.
        new SlotDef(IngredientType.WoodEar,      "목이",     MidRight, -189f, -200f, IngredientBinSize,
                    IngredientDir + "목이버섯 재료통.png", IngredientDir + "목이버섯 그릇용.png",
                    labelX: BinLabelOffsetX, crop: IngredientBinCrop, labelRotated: true),
    };

    [MenuItem("Tools/Ramen/Build Cooking Layout")]
    public static void Build()
    {
        // 플레이 중에는 아예 시작하지 않는다.
        //
        // 씬은 멀쩡히 만들어지지만 마지막 저장이 InvalidOperationException 으로 막히고,
        // 만든 것은 플레이를 멈추는 순간 통째로 버려진다. 겉으로는 "돌렸는데 아무것도 안 바뀜"
        // 으로만 보여서 원인을 찾기 어렵다. 2026-09-14 에 실제로 한 판을 그렇게 날렸다.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 플레이 중에는 돌릴 수 없습니다. " +
                             "플레이를 멈추고 다시 실행해 주세요. (지금 만들면 멈추는 순간 버려집니다)");
            return;
        }

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
        TopBarRefs topBar = BuildTopBar(canvas);
        // 팻말은 통보다 위에 그려져야 가려지지 않는다. 그리기 순서가 곧 만드는 순서라 먼저 만들 수 없어,
        // 오브젝트만 미리 만들어 슬롯에 넘기고 자리는 아래에서 옮긴다.
        SlotNameplate nameplate = BuildSlotNameplate(canvas, font);
        BuildSlots(CreateGroup("Slots", canvas), font, nameplate);

        // 면통은 그릇보다 먼저 만든다. 그릇이 위에 그려져야 면을 부을 때 그릇이 안 가려진다.
        BuildNoodlePot(canvas);

        Bowl bowl = BuildBowl(canvas);

        // 거부 안내는 그릇 위에 떠야 하므로 그릇보다 뒤에 만든다.
        bowl.toast = BuildIngredientToast(canvas);

        // 시치미·향미유는 그릇에 그림이 안 올라가서 넣었는지 알 길이 없다. 개수를 옆에 따로 센다.
        bowl.badges = BuildSeasoningBadges(canvas);

        // 팻말을 여기서 맨 뒤로 보낸다. 통과 그릇보다는 위에, 팝업들보다는 아래에 있어야 한다.
        // 통보다 아래면 팻말이 통에 가리고, 팝업보다 위면 팝업 위에 이름이 떠 버린다.
        nameplate.transform.SetAsLastSibling();

        // 튜토리얼 어두운 판.
        BuildTutorialDim(canvas);

        // 그릇도 평소에는 판 아래에 깔려 어둡다. 재료를 집어 옮기는 동안에만 올라와 밝아진다.
        // 늘 올려 두었더니 재료통이 켜져도 그릇과 밝기가 같아 "지금 여기" 가 안 읽혔다.
        BuildBowlLift(bowl.GetComponent<Image>());

        // 확인창 둘. 폐기·마무리 버튼은 그릇보다 먼저 만들어지므로 다 생긴 뒤에 연결한다.
        ConfirmDialogUI discardConfirm = BuildConfirmDialog(canvas, bowl, "DiscardConfirm",
                                                           "정말 폐기하시겠습니까?", false);
        UnityEventTools.AddVoidPersistentListener(
            DialogConfirmEvent(discardConfirm), new UnityEngine.Events.UnityAction(bowl.Discard));
        WireDialogButton(topBar.Discard, discardConfirm);

        ConfirmDialogUI submitConfirm = BuildConfirmDialog(canvas, bowl, "SubmitConfirm",
                                                          "마무리 하시겠습니까?", true);
        UnityEventTools.AddVoidPersistentListener(
            DialogConfirmEvent(submitConfirm), new UnityEngine.Events.UnityAction(bowl.Submit));
        WireDialogButton(topBar.Submit, submitConfirm);


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

        // 재료통 자물쇠. 통·토스트·DayManager 가 다 생긴 지금에야 이을 수 있다.
        WireSlotLocks(canvas, bowl.toast, orderSystem != null ? orderSystem.Day : null);

        // 자물쇠가 풀리는 연출과 그 팝업.
        BuildUnlockPopup(canvas, orderSystem != null ? orderSystem.Day : null);

        // 키 안내 아이콘. 조리 화면 위에 늘 떠 있다.
        BuildKeyHints(canvas);

        // 개발 키가 열려 있다는 표시. 잠겨 있으면 안 보인다.
        BuildDevKeyBadge(canvas);

        // 「꾹 눌러서 넘기기」 게이지. 시식 연출이 스스로 켰다 끈다.
        BuildHoldToSkip(canvas);

        // 100% 팻말. 결과창보다 앞이어야 한다.
        BuildPerfectSign(canvas);

        // 「주문마감!」. 100% 팻말과 같은 층이다 — 둘이 같이 뜨는 일은 없다.
        BuildClosedSign(canvas);

        // 화면 전환 판. 시작 화면보다 뒤여야 한다 — 덮개가 시작 화면까지 가리면
        // [게임시작] 을 누른 뒤 시작 화면이 걷히는 것이 안 보인다.
        BuildScreenFade(canvas);

        // 시작 화면을 GameManager 배선보다 먼저 만든다. 배선이 씬에서 찾아 꽂는 방식이라
        // 나중에 만들면 그때는 아직 없어서 빈 채로 남는다.
        BuildTitleScreen(canvas);

        // ESC 아이콘에 설정창을 잇는다. BuildKeyHints 안에서 하면 그때는 SettingsUI 가
        // 아직 없다 — 그것을 만드는 것이 방금 지나간 BuildTitleScreen 이다.
        WireEscHint(canvas);

        // 도입부 내레이션과 아이리스. 둘 다 전환 판보다 앞이라 나중에 만든다.
        BuildOpeningNarration(canvas);
        BuildDayTitle(canvas);
        BuildTutorialAsk(canvas);
        BuildIrisFade(canvas);

        WireGameManager(canvas, orderSystem, topBar);

        // DragLayer는 반드시 마지막. 그래야 드래그 고스트와 커서가 항상 모든 UI 위에 그려진다.
        // 이 그룹 자체에는 Image를 붙이지 않는다. 붙이면 화면 전체를 덮어 모든 클릭을 삼킨다.
        Transform dragLayer = CreateGroup("DragLayer", canvas);

        // 계층 순서만으로는 튜토리얼 어두운 판(100)을 못 넘는다. DragLayerOrder 설명 참고.
        // GraphicRaycaster 는 달지 않는다. 여기 있는 것들은 전부 raycastTarget 이 꺼져 있고,
        // 커서가 클릭을 먹으면 마우스 밑이 늘 커서라 아무것도 눌리지 않는다.
        var dragCanvas = Undo.AddComponent<Canvas>(dragLayer.gameObject);
        dragCanvas.overrideSorting = true;
        dragCanvas.sortingOrder = DragLayerOrder;

        BuildClickRipple(dragLayer);

        // 주문 내역(Tab)과 레시피북(B)은 빼 둔다. 둘 다 보면서 조리하는 창이라
        // 젓가락이 그대로 있어야 한다. 넣어 두면 책을 켠 순간 시스템 커서로 돌아가서,
        // 책 너머 재료통에 손을 올려도 집을 수 있는 것처럼 안 보인다.
        BuildCursor(dragLayer, new[]
        {
            popup.Root, finalPopup.Root, orderScreen.Root, orderResult.Root
        });

        // 그레인은 DragLayer 보다 뒤에 만든다. 나중에 만든 것이 위에 그려지므로
        // 커서와 드래그 고스트 위에까지 덮인다. 화면 전체에 고르게 깔려야 하기 때문이다.
        BuildScreenGrain(canvas);

        // 비네트와, 그 위로 올릴 UI. 둘 다 정렬 순서로만 자리를 잡으므로 만드는 차례는
        // 상관없지만, 끌어올릴 것들이 다 만들어진 뒤여야 이름으로 찾을 수 있다.
        BuildScreenVignette(canvas);
        LiftEdgeUi(canvas);

        // 시네마틱 동안 감출 판. 다 만들어진 뒤라야 전부 찾을 수 있어서 여기서 모은다.
        //
        // 두 군데에 흩어져 있다.
        //   조리 화면  주문 화면 그림이 화면 위쪽만 덮어서, 바 사이로 육수 냄비와 재료통이 비친다.
        //   주문 화면  "영업 시간 / 누적 수익" 판. 바를 푸는 동안 아래 끄트머리가 삐져나온다.
        WireCinematicHiding(canvas, orderScreen);

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

        // Overlay 가 아니라 카메라에 물린다. Overlay 는 카메라가 다 그린 뒤에 따로 얹혀서
        // 카메라 후처리를 통과하지 않는다 — 색보정(ScreenGrade)이 UI 에 안 먹는다.
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = EnsureScreenGrade();
        canvas.planeDistance = CanvasPlaneDistance;

        // 그리는 자리를 픽셀 경계에 맞춰 끊는다. 글자는 줄 길이가 홀수면 반칸에서 시작하는데,
        // 반칸에 걸린 그림은 어떤 획은 3픽셀, 어떤 획은 4픽셀로 찍혀 굵기가 들쭉날쭉해진다.
        canvas.pixelPerfect = true;

        // 배율은 PixelPerfectCanvas가 창 크기를 보고 정수로 정한다. 스케일러 설정은 건드리지 않는다.
        var pixelPerfect = Undo.AddComponent<PixelPerfectCanvas>(go);
        pixelPerfect.referenceResolution = new Vector2Int((int)DesignResolution.x, (int)DesignResolution.y);

        return go.transform;
    }

    // ── 화면 색보정 ───────────────────────────────────────────────
    //
    // 다 그려진 화면에 감마 곡선을 한 번 먹여 빛바랜 필름처럼 만든다.
    // 카메라에 붙는 후처리라 캔버스가 Screen Space - Camera 여야 통과한다.

    /// <summary>캔버스를 카메라 앞 얼마에 세울까. 카메라의 near 와 far 사이면 된다.</summary>
    private const float CanvasPlaneDistance = 100f;

    /// <summary>
    /// 기본 눈금. 0 이면 필터 없음이고, 그게 기본이다.
    ///
    /// 한동안 6(감마 1.26)이었다. 녹화에서 나왔던 색을 맞춘 값인데 실제 화면에서는
    /// 중간톤이 떠서 전체가 밝고 색이 빠져 보였다. 2026-09-14 에 꺼짐으로 되돌렸다.
    /// 그 색을 다시 보려면 설정에서 6 으로 올리면 된다.
    /// </summary>
    private const int ScreenGradeStep = 0;

    /// <summary>ScreenGrade 와 같은 값을 쓴다. 한쪽만 고치면 에디터와 실행 화면이 어긋난다.</summary>
    private const float ScreenGradeGammaPerStep = 0.21f;

    private const string ScreenFilterProfilePath = "Assets/Settings/ScreenFilter.asset";

    /// <summary>
    /// 비네트 세기 — 주문 화면 / 그 밖(조리 화면 등).
    ///
    /// **후처리가 아니라 그림 한 장이다.** URP Volume 의 Vignette 로 넣었다가 내렸다 —
    /// 카메라 후처리는 다 그려진 화면에 먹이므로 **UI 도 같이 눌린다.** 이 게임은 상단바·
    /// 키 힌트·팝업까지 전부 UI 라, 세기 0.5 에서 상단바 오른쪽과 키 힌트가 **0.00(완전히 검정)**
    /// 이 됐다. 낮추면 비네트가 안 보이고 올리면 UI 가 죽는다.
    ///
    /// 그림으로 내리면 정렬 순서로 UI 밑에 깔 수 있다(<see cref="VignetteOrder"/>).
    /// 값은 그림 알파 배수다 — 그림이 세기 0.5 로 구워져 있어 1 이 곧 0.5 다.
    /// </summary>
    private const float VignetteOrderScreen = 1f;

    /// <summary>
    /// 주문 화면 밖에서의 세기.
    ///
    /// 조리 화면은 **밝은 나무 카운터**라 같은 세기로 누르면 답답하고, 재료통이 화면
    /// 가장자리에 줄지어 있어서 구석이 어두워지면 집기 나빠진다.
    /// </summary>
    private const float VignetteOther = 0.56f;

    /// <summary>
    /// 비네트 판의 정렬 순서. **주문 화면(180)보다 위, 가장자리 UI(183)보다 아래**다.
    ///
    ///   조리 세계(0~101) · 주문 화면(180) → <b>비네트(182)</b> → 상단바·키 힌트·주문 상단 패널(183)
    ///   → 팝업(185) · 완벽 팻말(190) · 암전(300) · 주문마감(330)
    /// </summary>
    private const int VignetteOrder = 182;

    /// <summary>비네트 위로 끌어올리는 UI 의 정렬 순서. 팝업(185)보다는 아래여야 한다.</summary>
    private const int EdgeUiOrder = 183;

    private const float ScreenWarmth = 12f;

    /// <summary>
    /// 화면을 그릴 카메라를 찾아 후처리를 켜고, 색보정 Volume 을 달아 준다.
    ///
    /// 이 프로젝트는 GraphicsSettings 기본값은 빌트인인데 품질 설정에 URP 에셋이 물려 있어
    /// 실제로는 URP 로 돈다. 그래서 OnRenderImage 방식은 쓸 수 없고 Volume 으로 간다.
    /// </summary>
    private static Camera EnsureScreenGrade()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();

        if (cam == null)
        {
            var go = new GameObject("Main Camera", typeof(Camera));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.tag = "MainCamera";

            cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        var data = cam.GetComponent<UniversalAdditionalCameraData>();
        if (data == null) data = Undo.AddComponent<UniversalAdditionalCameraData>(cam.gameObject);

        // 둘 다 꺼져 있을 때만 후처리를 안 건다. 예전에는 감마 눈금만 봤는데,
        // 그러면 색온도를 켜도 카메라가 후처리를 안 돌려 아무 일도 안 일어난다.
        // 비네트는 이제 후처리가 아니라 그림이라 여기 없다.
        data.renderPostProcessing = ScreenGradeStep > 0
                                    || !Mathf.Approximately(ScreenWarmth, 0f);

        BuildScreenFilter();

        return cam;
    }

    /// <summary>
    /// 색보정 Volume. 전역이라 카메라가 어디에 있든 걸린다.
    ///
    /// 프로파일은 에셋으로 둔다. 실행 중에는 ScreenGrade 가 Volume.profile 로 사본을 떠서
    /// 만지므로, 플레이하며 고친 값이 이 에셋에 남지 않는다.
    /// </summary>
    private static void BuildScreenFilter()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ScreenFilterProfilePath);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ScreenFilterProfilePath);
        }

        LiftGammaGain grade;
        if (!profile.TryGet(out grade))
        {
            grade = profile.Add<LiftGammaGain>(true);

            // 프로파일 안의 효과는 따로 하위 에셋으로 박아 줘야 파일에 남는다.
            // 이걸 빼먹으면 에디터에서는 멀쩡히 보이다가, 다시 읽을 때 효과가 통째로 사라진다.
            grade.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(grade, profile);
        }

        grade.active = ScreenGradeStep > 0;
        grade.gamma.overrideState = true;
        grade.gamma.value = new Vector4(1f, 1f, 1f, ScreenGradeGammaPerStep * ScreenGradeStep);

        // 비네트는 여기 없다. 후처리로 걸면 UI 까지 눌려서 그림으로 내렸다(ScreenVignette).

        // 색 온도 — 전구 밑 느낌은 비네트보다 이쪽이 더 많이 만든다.
        WhiteBalance warmth;
        if (!profile.TryGet(out warmth))
        {
            warmth = profile.Add<WhiteBalance>(true);
            warmth.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(warmth, profile);
        }

        warmth.active = !Mathf.Approximately(ScreenWarmth, 0f);
        warmth.temperature.overrideState = true;
        warmth.temperature.value = ScreenWarmth;

        EditorUtility.SetDirty(grade);
        EditorUtility.SetDirty(warmth);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        GameObject existing = GameObject.Find(ScreenFilterName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        // 예전에 카메라에 붙여 두었던 것까지 싹 치운다. 하나라도 남으면 ScreenGrade.Instance 가
        // 그쪽을 가리킬 수 있고, 그 컴포넌트에는 Volume 이 안 꽂혀 있어 설정이 아무 데도 안 닿는다.
        foreach (var stale in Object.FindObjectsByType<ScreenGrade>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.DestroyObjectImmediate(stale);
        }

        var go = new GameObject(ScreenFilterName, typeof(Volume));
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);

        var volume = go.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = profile;

        var script = Undo.AddComponent<ScreenGrade>(go);
        SetPrivateReference(script, "volume", volume);
        SetPrivateInt(script, "step", ScreenGradeStep);

    }

    private const string ScreenFilterName = "ScreenFilter";

    /// <summary>
    /// 화면을 짜는 640×360 판. 모든 UI가 이 안에 들어간다.
    ///
    /// 캔버스 자체는 창 비율을 그대로 받아서 16:9가 아니면 640×360보다 넓거나 높아진다.
    /// 그때 요소를 캔버스 가장자리에 직접 붙여 두면 창 크기마다 간격이 벌어져 구도가 흔들린다.
    /// 그래서 크기가 고정된 판을 하나 깔고 거기에 붙인다. 남는 자리는 여백이 된다.
    /// </summary>
    /// <summary>
    /// 화면 전체를 덮는 그레인 한 장.
    ///
    /// 판(Frame) 크기 그대로 만든다. 셰이더가 uv 를 판 크기 격자로 끊어 알갱이를
    /// 게임 픽셀에 맞추므로, 상자 크기와 격자가 같아야 1:1 이 된다.
    /// </summary>
    /// <summary>
    /// 비네트 판. 화면 가장자리를 눌러 가운데로 눈을 모은다.
    ///
    /// **후처리가 아니라 그림이다.** 이유는 <see cref="VignetteOrderScreen"/> 참고 —
    /// 후처리로 걸면 UI 까지 눌려서 상단바와 키 힌트가 새까매진다.
    ///
    /// 화면 전체(ScreenCover)로 늘려 쓴다. 매끈한 그라데이션이라 늘려도 계단이 안 진다.
    /// </summary>
    private static void BuildScreenVignette(Transform canvas)
    {
        Image vignette = CreateImage("ScreenVignette", canvas, Center, Vector2.zero, DesignResolution,
                                     Color.white, LoadSprite(GeneratedDir + "비네트.png"));
        vignette.raycastTarget = false;

        // **판 밖으로 나가게 깔면 안 된다.** 처음에 ScreenCover(1920x1080 캔버스 단위)로 깔았더니
        // 화면에 보이는 것은 960x540 이라 **그라데이션의 안쪽 절반만 보였다** — 세기를 아무리
        // 올려도 옅게만 깔린다. 판(Frame)에 딱 맞춰 늘려 두면 화면 가장자리에 굽이 끝이 온다.
        RectTransform vr = vignette.rectTransform;
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = Vector2.zero;
        vr.offsetMax = Vector2.zero;

        var own = Undo.AddComponent<Canvas>(vignette.gameObject);
        own.overrideSorting = true;
        own.sortingOrder = VignetteOrder;

        var script = Undo.AddComponent<ScreenVignette>(vignette.gameObject);
        SetPrivateFloat(script, "orderStrength", VignetteOrderScreen);
        SetPrivateFloat(script, "otherStrength", VignetteOther);
    }

    /// <summary>
    /// 화면 가장자리에 붙은 UI 를 비네트 위로 끌어올린다.
    ///
    /// 가운데에 뜨는 것(팝업·주문서·레시피북)은 비네트가 거의 안 닿아서 그대로 둔다.
    /// 문제는 **가장자리에 붙박인 것들**이다 — 세기 0.5 에서 재 보면 상단바 오른쪽과
    /// 키 힌트가 0.00(완전히 검정)이 된다.
    ///
    /// **GraphicRaycaster 를 같이 단다.** Canvas 를 붙이는 순간 그 아래 Graphic 들이 뿌리 캔버스가
    /// 아니라 이 캔버스에 등록되고, GraphicRaycaster 는 제가 붙은 캔버스 것만 훑는다. 그래서
    /// 여기서 빠뜨리면 그 안의 버튼에 클릭이 <b>아예</b> 안 닿는다 — 눌림도 호버 테두리도 안 뜬다.
    ///
    /// 2026-09-16 에 상단바(TopBar)가 그랬다. 비네트 위로 올리면서 Canvas 만 달았더니
    /// 폐기(휴지통)·마무리 버튼이 통째로 죽었다. 배선도 `raycastTarget` 도 멀쩡한데 눌리지만
    /// 않아서 원인이 안 보였다. LiftCanvas·LiftPopup 은 처음부터 같이 달고 있다 — 같은 실수를
    /// 거기서 이미 한 번 하고 적어 둔 것이었는데 이쪽만 새로 팠다.
    /// </summary>
    private static void LiftEdgeUi(Transform canvas)
    {
        // 조리 화면의 상단바·키 힌트와, 주문 화면이 따로 쥔 상단 패널 둘.
        string[] names = { "TopBar", "KeyHint_Tab", "KeyHint_Book", "KeyHint_Esc", "DayTimePanel", "RevenuePanel" };

        foreach (string name in names)
        {
            Transform found = FindDeep(canvas, name);
            if (found == null)
            {
                Debug.LogWarning("[RamenLayoutBuilder] 비네트 위로 올릴 것을 찾지 못했습니다: " + name);
                continue;
            }

            var own = found.GetComponent<Canvas>();
            if (own == null) own = Undo.AddComponent<Canvas>(found.gameObject);

            own.overrideSorting = true;
            own.sortingOrder = EdgeUiOrder;

            // 캔버스를 달았으면 레이캐스터도 달아야 한다. 위 주석 참고.
            if (found.GetComponent<GraphicRaycaster>() == null)
                Undo.AddComponent<GraphicRaycaster>(found.gameObject);
        }
    }

    private static void BuildScreenGrain(Transform canvas)
    {
        Material material = GrainMaterial();
        if (material == null) return;

        Image grain = CreateImage("ScreenGrain", canvas, Center, Vector2.zero,
                                  DesignResolution, Color.white);
        grain.material = material;
        grain.raycastTarget = false;

        var script = Undo.AddComponent<ScreenGrain>(grain.gameObject);
        SetPrivateReference(script, "shakeTarget", canvas as RectTransform);
        SetPrivateFloat(script, "noiseSize", GrainNoiseSize);
    }

    /// <summary>그레인 머티리얼. 없으면 만들고, 노이즈 그림을 꽂아 둔다.</summary>
    private static Material GrainMaterial()
    {
        Shader shader = Shader.Find("Ramen/PixelGrain");
        if (shader == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] Ramen/PixelGrain 셰이더를 찾지 못했습니다. " +
                             "Assets/Shaders/PixelGrain.shader 가 있는지 확인해 주세요.");
            return null;
        }

        Texture2D noise = GrainNoiseTexture();
        if (noise == null) return null;

        const string path = "Assets/Shaders/PixelGrain.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetTexture("_NoiseTex", noise);
        material.SetFloat("_NoiseSize", GrainNoiseSize);
        EditorUtility.SetDirty(material);

        return material;
    }

    /// <summary>
    /// 그레인용 백색잡음. 구워서 Generated 폴더에 둔다.
    ///
    /// URP 패키지에도 필름 그레인 그림이 있지만 쓰지 않는다. 그쪽은 부드럽게 뭉개진 그레인이라
    /// 칸마다 값이 뚝뚝 끊기지 않는다. 여기서는 칸 하나가 텍셀 하나라 이웃과 확실히 달라야 한다.
    /// </summary>
    private static Texture2D GrainNoiseTexture()
    {
        const int N = GrainNoiseSize;
        var px = new Color[N * N];

        // 씨앗을 고정한다. 빌더를 돌릴 때마다 그림이 달라지면 무의미한 변경이 커밋에 쌓인다.
        var random = new System.Random(20260910);
        for (int i = 0; i < px.Length; i++)
        {
            float v = (float)random.NextDouble();
            px[i] = new Color(v, v, v, 1f);
        }

        string path = SaveGenerated("GrainNoise", EncodePng(px, N, N));
        if (path == null) return null;

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null &&
            (importer.textureType != TextureImporterType.Default
             || importer.wrapMode != TextureWrapMode.Repeat
             || importer.filterMode != FilterMode.Point
             || importer.mipmapEnabled
             || importer.textureCompression != TextureImporterCompression.Uncompressed))
        {
            // 반복해서 읽으므로 Repeat 여야 한다. Clamp 면 가장자리 한 줄이 화면 전체에 늘어난다.
            // Point 가 아니면 이웃 텍셀이 섞여 칸 경계가 흐려지고, 알갱이가 게임 픽셀과 어긋난다.
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

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
        Sprite art = LoadPhotoSprite(ScreenDir + "제조화면 배경.png");
        if (art != null)
        {
            img.sprite = art;
            img.color = Color.white;
        }
        else
        {
            // 사진이 없으면 코드로 만든 나무 널판을 깐다.
            // Tiled 는 그림을 원본 크기 그대로 반복해 찍으므로 늘어나지 않는다.
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
                img.color = Hex("#F5E9D0");
            }
        }

        img.raycastTarget = false;   // 배경이 클릭을 먹지 않도록
    }



    /// <summary>나중에 연결해야 하는 것들을 묶어 돌려준다.</summary>
    /// <summary>
    /// 조리 화면 상단바.
    ///
    /// 판 크기·아이콘·글자를 주문 화면(BuildOrderScreen)과 똑같이 맞춘다. 두 화면이 겹쳐
    /// 있다가 하나만 걷히는 구조라, 상단바가 서로 다르면 넘어가는 순간 티가 난다.
    /// 글자도 주문 화면과 같은 TMP 를 쓴다. legacy Text 는 픽셀 폰트를 또렷하게 못 낸다.
    /// </summary>
    private static TopBarRefs BuildTopBar(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform bar = CreateGroup("TopBar", canvas);

        IconSprites icons = LoadIconSprites();

        // 왼쪽 구석은 Tab·B 키 안내가 쓴다(BuildKeyHints). 예전에 여기 있던 주문 확인 ? 버튼은
        // 뺐다 — Tab 이 여는 것과 같은 주문서를 토글하던 마우스용 창구라 겹쳤다.

        // 날짜와 영업 시각. 왼쪽에 시계 아이콘이 붙은 판이라 글자를 그만큼 오른쪽으로 민다.
        // 키 안내 둘(아이콘+글자, 오른끝 130)을 피해 오른쪽으로 물러나 있다.
        Image day = CreateImage("DayPanel", bar, TopLeft, new Vector2(255f, -24f),
                                new Vector2(200f, PanelBarHeight), Color.white, TimeBarSprite(), PanelScale);
        // 시계 아이콘. 손님이 갈 때마다 GameManager 가 DayClockIcon 으로 조각을 채운다.
        Image dayIcon = AttachPanelIcon(day, TimeIconSprite(), new Vector2(40f, 36f));
        var dayClock = dayIcon != null ? Undo.AddComponent<DayClockIcon>(dayIcon.gameObject) : null;
        var dayText = CreateTmpText("Label", day.transform, Center, Vector2.zero,
                                    new Vector2(174f, 38f), "1일차", TextHead, tmpFont);

        // 마무리 버튼. 예전에는 그릇을 끌어다 놓는 영역이었고 글자도 "완성하기" 였다.
        Sprite submitBar = SubmitBarSprite();
        Image submit = CreateImage("SubmitZone", bar, TopCenter, new Vector2(0f, -24f), new Vector2(200f, 36f),
                                   Color.white, submitBar);
        submit.type = Image.Type.Sliced;
        Undo.AddComponent<SubmitZone>(submit.gameObject);
        var submitLabel = CreateTmpText("Label", submit.transform, Center, Vector2.zero,
                                        new Vector2(174f, 38f), "마무리", TextTitle, tmpFont);

        // 튜토리얼 안내 테두리. 재료를 다 넣어 제출만 남았을 때 켜진다.
        // 글자보다 뒤에 만들어야 테두리가 "마무리" 를 덮지 않는다.
        //
        // **버튼 그림과 같은 것을 넘겨야 한다.** 테두리를 그림 모양에서 떠 오므로,
        // 버튼만 갈고 여기를 안 고치면 옛 초록 바 모양의 테가 남는다.
        BuildSubmitOutline(submit, submitBar);
        submitLabel.color = GoldInkColor;

        // 금일 수익. 재료비와 자본은 기획 확정으로 제거되어 매출만 표시한다.
        //
        // 좌우가 꽉 찬 자리라 숫자로 맞춰야 한다(캔버스 x, -480~480).
        //   마무리 -100~100 · 휴지통 436~472 가 양옆에 고정돼 있다.
        //   판 290 을 -201 에 두면 134~424. 왼쪽 12, 오른쪽 12 가 뜬다.
        //   **아이콘이 판 왼쪽 외곽선에 걸터앉아 18칸 더 나간다**(AttachPanelIcon) — 116 까지다.
        //   판을 306 으로 넓혔다가 아이콘이 마무리를 14칸 파고들었다.
        //
        // 글자는 아이콘을 피해 오른쪽으로 8 민다. 가장 긴 글은 「금일 수익 : 80,000₩」 246칸이다
        // (5일차 8명 x 10,000원이 최대라 여섯 자리를 넘지 않는다).
        Image revenue = CreateImage("RevenuePanel", bar, TopRight, new Vector2(-201f, -24f),
                                    new Vector2(290f, PanelBarHeight), Color.white, MoneyBarSprite(), PanelScale);
        AttachPanelIcon(revenue, MoneyIconSprite(), new Vector2(36f, 28f));
        // 목표까지 같이 싣느라 한 칸 작다(TextHead 16 = 갈무리7 x 2).
        // 「금일 수익 : 80,000 / 56,000₩」이 24 로는 366칸이라 상자(254)에 어떻게 해도 안 들어간다.
        // 16 이면 244 로 들어가고, 판을 넓힐 자리는 이미 없다 — 왼쪽은 마무리, 오른쪽은 휴지통이다.
        var revenueText = CreateTmpText("Label", revenue.transform, Center, new Vector2(8f, 0f),
                                        new Vector2(254f, 38f), "금일 수익 : 0 / 0₩", TextHead, tmpFont);

        // 폐기 버튼. onClick은 그릇이 생긴 뒤 WireDiscardButton에서 붙인다.
        Image discard = CreateImage("DiscardButton", bar, TopRight, new Vector2(-26f, -24f), new Vector2(36f, 36f),
                                    Color.white, icons.Trash);

        // 손님 대사 줄. 주문 화면(B)이 아직 없어서 조리 화면 위에 글자로만 띄운다.
        return new TopBarRefs { Root = bar, Discard = discard, Submit = submit,
                                DayText = dayText, DayClock = dayClock, RevenueText = revenueText };
    }

    /// <summary>
    /// 슬롯 하나 = 통 그림 + 아래에 이름.
    /// 고체는 IngredientSlot, 액체·조미료는 LiquidSlot 이 붙는다. 둘 다 그릇으로 끌어다 놓는다.
    /// </summary>
    /// <summary>
    /// 하루 마감 때 뜨는 정산 팝업. 내용 갱신과 열고 닫기는 B의 DailyResultUI가 한다.
    ///
    /// 2026-09-15 부터 **B가 만든 프리팹을 얹는다**(TodayReciept.prefab). 예전에는 판·글자·
    /// 버튼을 여기서 코드로 다 만들었는데, 그러면 B가 에디터에서 화면을 손봐도 빌더를 한 번
    /// 돌리는 순간 씬이 새로 만들어지면서 통째로 날아간다.
    ///
    /// 이제 모양은 프리팹이 쥐고 빌더는 **자리와 배선만** 잡는다. B가 프리팹을 고치면
    /// 씬에 저절로 따라오고, 빌더를 돌려도 안 깨진다.
    ///
    /// <see cref="PrefabUtility.InstantiatePrefab"/> 을 쓴다. Object.Instantiate 로 띄우면
    /// 프리팹과 끊긴 복사본이 되어 B의 수정이 안 따라온다.
    /// </summary>
    private static ResultPopupRefs BuildResultPopup(Transform canvas)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ResultPopupPrefab);
        if (asset == null)
        {
            Debug.LogError("[RamenLayoutBuilder] 정산 팝업 프리팹이 없습니다: " + ResultPopupPrefab);
            return null;
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, canvas);
        Undo.RegisterCreatedObjectUndo(root, UndoLabel);

        // 프리팹이 어떤 앵커로 저장돼 있든 화면 한가운데에 세운다.
        // 뿌리는 화면을 덮는 자리다(어두운 판이 그 밑에 깔린다). 영수증 그림은 자식이 쥔다.
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Center;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = ScreenCover;

        LiftPopup(root);

        // 시작할 때는 닫혀 있어야 한다. DailyResultUI.Awake도 끄지만, 에디터에서도 가려지지 않게 여기서 끈다.
        root.SetActive(false);

        return new ResultPopupRefs
        {
            Root = root,
            Title = PrefabText(root, "CurrentDay"),
            TargetProfit = PrefabText(root, "TodayGoal"),
            Profit = PrefabText(root, "TodayProfit"),
            TotalProfit = PrefabText(root, "TotalProfit"),
            Accuracy = PrefabText(root, "TodayAccuracy"),
            Perfect = PrefabText(root, "PerfectRamen"),
            Confirm = PrefabButton(root, "Ok_Btn"),
            Retry = PrefabButton(root, "Retry_Btn"),
        };
    }

    /// <summary>프리팹 어딘가에 있는 글자를 이름으로 찾는다. 못 찾으면 경고를 남기고 null.</summary>
    private static TextMeshProUGUI PrefabText(GameObject root, string name)
    {
        Transform found = FindDeep(root.transform, name);
        if (found == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 정산 프리팹에서 " + name + " 을 찾지 못했습니다.");
            return null;
        }

        return found.GetComponent<TextMeshProUGUI>();
    }

    /// <summary>프리팹 어딘가에 있는 버튼을 이름으로 찾는다. 못 찾으면 경고를 남기고 null.</summary>
    private static Button PrefabButton(GameObject root, string name)
    {
        Transform found = FindDeep(root.transform, name);
        if (found == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 정산 프리팹에서 " + name + " 을 찾지 못했습니다.");
            return null;
        }

        return found.GetComponent<Button>();
    }

    /// <summary>
    /// 이름으로 자손을 뒤진다. Transform.Find 는 바로 아래 자식만 보기 때문에,
    /// B가 프리팹 안에서 오브젝트를 한 겹 더 묶어도 안 깨지도록 깊이 들어간다.
    /// </summary>
    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }

        return null;
    }

    /// <summary>
    /// 손님 한 명분 결과창. 제출 직후에 뜨고 [확인]을 눌러야 다음 손님으로 넘어간다.
    /// </summary>
    private static OrderResultRefs BuildOrderResult(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();
        IconSprites icons = LoadIconSprites();

        Transform root = CreateGroup("OrderResult", canvas);
        LiftPopup(root.gameObject);

        var backdrop = CreateImage("Backdrop", root, Center, Vector2.zero, ScreenCover, new Color(0f, 0f, 0f, 0.7f));
        backdrop.raycastTarget = true;

        // 판은 파이썬으로 구운 종이 한 장이다(Tools/make_result_panel.py, 300x330).
        //
        // 예전에는 크림색 사각판(#FFF8E7 + TextBox.png)에 주황 막대였다. **게임에서 제일 자주 보는
        // 팝업**인데 하루 정산(영수증)·5일 결산(장부)과 결이 달라 혼자 다른 게임 창처럼 보였다.
        //
        // 종이 셋을 가장자리로 가른다 — 정산표는 아래만 톱니에 오른쪽 위 접힘, 최종결산판은
        // 아래만 톱니에 왼쪽 위 접힘, 이쪽은 **위아래 둘 다 톱니**(두루마리에서 뜯어낸 전표).
        //
        // 줄 사이는 예전처럼 고르게 둔다. 판이 290 → 330 으로 커진 만큼 아래로 밀었다.
        //
        //   판 위끝 +165
        //   정확도 명패   +96 ~ +140
        //   이모지        +32 ~  +80
        //   얼굴·한마디   -20 ~  +24   (두 줄 기준. 사이는 전부 8칸씩이다)
        //   보상          -51 ~  -29
        //   누적          -70 ~  -59
        //   확인 버튼    -153 ~  -78
        //   판 아래끝 -165
        //
        // 판은 가운데가 아니라 **오른쪽**에 선다. 왼쪽 두 자리는 내가 만든 그릇과 주문서 몫이다.
        Image panel = CreateImage("Panel", root, Center, new Vector2(ResultPanelX, 0f),
                                  new Vector2(ResultPanelWidth, ResultPanelHeight),
                                  Color.white, LoadSprite(GeneratedDir + "주문결과판.png"));

        // 정확도 명패. **숫자는 안 굽는다** — 매번 바뀐다. 판만 굽고 TMP 가 그 위에 쓴다.
        Image accuracyBar = CreateImage("AccuracyBar", panel.transform, Center, new Vector2(0f, 118f),
                                        new Vector2(236f, 44f), Color.white,
                                        LoadSprite(GeneratedDir + "정확도명패.png"));
        var accuracy = CreateTmpText("AccuracyText", accuracyBar.transform, Center, Vector2.zero,
                                     new Vector2(216f, 30f), "정확도 : 0%", TextTitle, tmpFont);
        accuracy.color = ResultInkColor;

        // 이모지는 아이콘 아틀라스에서 잘라 쓴다. 표정은 OrderResultUI가 정확도로 고른다.
        Sprite[] faces = icons.Faces;
        Image emoji = CreateImage("Emoji", panel.transform, Center, new Vector2(0f, 56f),
                                  new Vector2(48f, 48f), Color.white,
                                  faces != null && faces.Length > 0 ? faces[0] : null);
        emoji.preserveAspect = true;

        // 손님 초상. 그림은 OrderResultUI 가 손님마다 갈아 끼우므로 여기서는 자리만 잡는다.
        // 48 칸인 까닭은 얼굴 40 에 흰 여백 2 와 테 2 를 둘러 구웠기 때문이다.
        Image face = CreateImage("CustomerFace", panel.transform, Center, new Vector2(-108f, 2f),
                                 new Vector2(48f, 48f), Color.white,
                                 LoadSprite(GeneratedDir + "손님얼굴/Polite.png"));

        // 한마디는 얼굴 오른끝(-84)에서 12 칸 떨어져 시작한다. 붙여 두면 얼굴 테와 글자가
        // 한 덩어리로 보인다. 가운데 정렬이 아니라 **왼쪽 정렬**이다 — 말풍선처럼 읽혀야 한다.
        //
        // **두 줄이 기본이다.** 반응 90줄을 재 보니 40줄이 206칸을 넘는다. 제일 긴 줄이
        // 344칸이라 두 줄이면 다 들어간다 — 세 줄짜리는 없다. 그래서 56칸으로 잡았다.
        //
        // 튜토리얼 안내만 세 줄이 되는데(원래 두 줄인 글이 좁아진 상자에서 한 번 더 접힌다),
        // 그때는 보상·누적이 감춰져 있고 TMP 가 상자 밖으로 흘려 그려서 부딪히지 않는다.
        var line = CreateTmpText("CustomerLine", panel.transform, Center, new Vector2(31f, 2f),
                                 new Vector2(206f, 56f), "잘 먹었습니다.", TextHead, tmpFont);
        line.color = ResultQuoteColor;
        line.alignment = TextAlignmentOptions.Left;

        // 보상과 누적은 좌우로 나란히 두었다가 위아래로 쌓았다. 나란히 두면 둘이 같은 무게로
        // 읽혀서, 정작 기분 좋아야 할 보상이 왼쪽 구석에 밀린다.
        var reward = CreateTmpText("RewardText", panel.transform, Center, new Vector2(0f, -40f),
                                   new Vector2(200f, 28f), "+ 0₩", TextTitle, tmpFont);
        reward.color = ResultRewardColor;

        var revenue = CreateTmpText("RevenueText", panel.transform, Center, new Vector2(0f, -64f),
                                    new Vector2(200f, 18f), "금일 수익 0₩", TextBody, tmpFont);
        revenue.color = ResultFaintColor;

        // 정산 팝업·최종 결과창과 같은 버튼 그림이다. **글자가 그림에 박혀 있어** 따로 안 얹는다.
        Image confirmImage = CreateImage("ConfirmButton", panel.transform, Center, new Vector2(0f, -116f),
                                         new Vector2(224f, 75f), Color.white,
                                         LoadPhotoSprite(UiDir + "버튼_확인.png"));
        Button confirm = Undo.AddComponent<Button>(confirmImage.gameObject);
        confirm.targetGraphic = confirmImage;
        StyleButton(confirm);

        // ── 왼쪽 두 자리: 내가 만든 그릇과 받은 주문서 ──────────────
        //
        // 점수만 띄우면 무엇을 틀렸는지 알 길이 없다. 낸 그릇을 그대로 옮겨 놓고 주문서를
        // 옆에 세워, 손님 한마디가 가리키는 쪽("뭔가 빠진 것 같다")을 직접 견주어 보게 한다.
        //
        // 그릇 그림은 여기서 넣지 않는다. 제출하는 순간의 조리대 그릇을 통째로 복제해 온다
        // (OrderResultUI.CaptureBowl). 그래서 처음에는 꺼 둔다.
        Image servedBowl = CreateImage("ServedBowl", root, Center, new Vector2(ResultBowlX, 0f),
                                       new Vector2(ResultBowlSize, ResultBowlSize), Color.white);
        servedBowl.preserveAspect = true;
        servedBowl.raycastTarget = false;
        servedBowl.enabled = false;

        // 주문서는 Tab 으로 여는 것과 같은 종이다. 다만 미끄러지지도 흐려지지도 않는다.
        //
        // 종이 길이는 주문마다 다르다(ResultOrderNote 가 매번 다시 잡는다). 피벗이 가운데라
        // 길어지든 짧아지든 위아래로 똑같이 자라, 그릇·판과 **세로 가운데**가 늘 맞는다.
        // 위끝을 판에 맞춰 두었더니 짧은 주문에서 종이만 위로 쏠려 아래가 휑했다.
        OrderNoteRefs note = BuildNotePaper(root, new Vector2(ResultNoteX, 0f));

        root.gameObject.SetActive(false);

        return new OrderResultRefs
        {
            Root = root.gameObject,
            ServedBowl = servedBowl,
            Note = note,
            Accuracy = accuracy,
            Reward = reward,
            Revenue = revenue,
            CustomerLine = line,
            Emoji = emoji,
            Faces = faces,
            CustomerFace = face,
            Confirm = confirm
        };
    }

    /// <summary>Icon.png에서 잘라 낸 조각들.</summary>
    private class IconSprites
    {
        public Sprite SubmitBar;    // 초록 바 — 제출하기 영역
        public Sprite Trash;        // 휴지통 — 폐기 버튼
        public Sprite AccuracyBar;  // 살색 바 — 정확도 표시
        public Sprite[] Faces;      // 웃음 · 무표정 · 화남

        // 나무 바 두 장(Icon_Wood · Icon_WoodDown)은 여기서 뺐다. 시작 화면 버튼이
        // 따로 구운 판(버튼_타이틀)으로 갈아타면서 읽는 곳이 없어졌다.
        // 아틀라스의 칸 자체는 그대로 둔다 — 그림을 지우는 것은 코드가 할 일이 아니다.
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

        // CS0618: TextureImporter.spritesheet 은 폐기 예고 상태다. 유니티는 ISpriteEditorDataProvider
        // 를 쓰라고 하지만, 새 API 는 칸마다 GUID 를 직접 관리해야 해서 잘못 건드리면 씬이 물고 있는
        // 스프라이트 참조가 끊긴다. 6000.3 에서는 아직 제대로 동작하니 지금은 경고만 끈다.
        // 정말 막히면 경고가 아니라 에러(CS0619)로 바뀌어 바로 드러난다. 그때 옮기면 된다.
        #pragma warning disable 0618

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
        #pragma warning restore 0618

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
            Faces = new[] { pick("Face_0"), pick("Face_1"), pick("Face_2") }
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
        Transform root = CreateGroup("OrderNote", canvas);
        LiftOverlay(root.gameObject);

        // 자리는 OrderNoteUI 가 정한다. 여기서는 화면 오른쪽 바깥(숨은 자리)에 둔다.
        OrderNoteRefs refs = BuildNotePaper(root, new Vector2(580f, 0f));
        refs.Root = root.gameObject;

        root.gameObject.SetActive(false);
        return refs;
    }

    /// <summary>
    /// 영수증 종이 한 장. Tab 으로 여는 주문 내역과 결과창에 붙박이로 서는 주문서가 같이 쓴다.
    /// 여닫는 방식과 서는 자리는 부르는 쪽이 정한다.
    /// </summary>
    private static OrderNoteRefs BuildNotePaper(Transform parent, Vector2 position)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        // 종이.
        //
        // 예전에는 64x64 그림에서 40x49 를 잘라 통째로 6배 확대했다. 그러면 원본의 1픽셀 선이
        // 전부 6픽셀이 되어, 9-슬라이스로 1픽셀을 지키는 말풍선·상단바와 굵기가 따로 놀았다.
        // 지금은 종이도 9-슬라이스라 아무리 늘려도 테두리가 1픽셀이다.
        //
        // 높이는 여기서 정하지 않는다. 대사 길이에 따라 종이를 쥔 쪽이 매번 다시 잡는다.
        // 여기 넣는 값은 씬에서 눈으로 볼 때 쓰는 임시값이다.
        Image sheet = CreateImage("Paper", parent, Center, position,
                                  new Vector2(NoteWidth, NoteChromeHeight + 130f),
                                  Color.white, NotePaperSprite());
        sheet.type = Image.Type.Sliced;
        sheet.pixelsPerUnitMultiplier = 1f;
        sheet.raycastTarget = false;

        // 마우스를 올리면 종이가 흐려진다. 글자·선·톱니까지 한꺼번에 흐려져야 하므로
        // 낱장마다 색을 만지지 않고 종이에 CanvasGroup 을 하나 씌운다.
        //
        // blocksRaycasts 를 꺼 둔다. 종이는 재료통과 그릇을 덮고 있는데, 이걸 켜면
        // 안에 있는 것이 전부 클릭을 가로채 뒤를 못 만지게 된다.
        var noteFade = Undo.AddComponent<CanvasGroup>(sheet.gameObject);
        noteFade.blocksRaycasts = false;
        noteFade.interactable = false;

        // 머리글.
        var header = CreateTmpText("HeaderText", sheet.transform, TopLeft,
                                   new Vector2(NoteMargin, -8f), new Vector2(NoteTextWidth, 14f),
                                   "주문서", NoteTitleFontSize, tmpFont);
        header.alignment = TextAlignmentOptions.TopLeft;
        header.rectTransform.pivot = TopLeft;
        header.rectTransform.anchoredPosition = new Vector2(NoteMargin, -8f);

        // 구분선. 4x1 짜리 점선 한 칸을 가로로 깐다. 선 하나를 통째로 늘리면
        // 점 간격이 종이 폭에 따라 들쭉날쭉해진다.
        CreateNoteRule(sheet.transform, TopLeft, -28f);

        // 영수증 머리의 정보줄. 값은 OrderNoteUI 가 채운다.
        //
        // 라멘 종류는 일부러 넣지 않는다. 무슨 라멘인지 알아내는 것이 이 게임의 기본 과제라
        // 여기 적어 두면 답을 알려 주는 꼴이 된다.
        var dayLabel = CreateTmpText("DayLabel", sheet.transform, TopLeft,
                                     new Vector2(NoteMargin, -34f), new Vector2(60f, 14f),
                                     "1일차", TextSmall, tmpFont);
        dayLabel.alignment = TextAlignmentOptions.TopLeft;
        dayLabel.color = NoteFadeColor;
        dayLabel.rectTransform.pivot = TopLeft;
        dayLabel.rectTransform.anchoredPosition = new Vector2(NoteMargin, -34f);

        var customerLabel = CreateTmpText("CustomerLabel", sheet.transform, TopRight,
                                          new Vector2(-NoteMargin, -34f), new Vector2(94f, 14f),
                                          "1번째 손님", TextSmall, tmpFont);
        customerLabel.alignment = TextAlignmentOptions.TopRight;
        customerLabel.color = NoteFadeColor;
        customerLabel.rectTransform.pivot = TopRight;
        customerLabel.rectTransform.anchoredPosition = new Vector2(-NoteMargin, -34f);

        CreateNoteRule(sheet.transform, TopLeft, -53f);

        // 손님 대사. 위에 붙여 두고 아래로 자란다. 높이는 OrderNoteUI 가 매번 다시 잡는다.
        var dialogue = CreateTmpText("DialogueText", sheet.transform, TopLeft,
                                     new Vector2(NoteMargin, -61f), new Vector2(NoteTextWidth, 130f),
                                     "", NoteFontSize, tmpFont);
        dialogue.alignment = TextAlignmentOptions.TopLeft;
        dialogue.rectTransform.pivot = TopLeft;
        dialogue.rectTransform.anchoredPosition = new Vector2(NoteMargin, -61f);
        ApplyPixelLineSpacing(dialogue, NoteLineHeight);

        // 자동 크기 조절을 껐다. TMP가 아무 값이나 골라 버리면 8·10·12·15 규칙이 그 자리에서
        // 깨져 글자에 회색이 낀다. 넘치면 글자가 아니라 종이를 늘린다.
        dialogue.enableAutoSizing = false;

        // 아래 구분선은 종이 아래변에 붙인다. 종이가 길어져도 늘 맨 아래에 있다.
        CreateNoteRule(sheet.transform, BottomLeft, 5f);

        // 톱니.
        //
        // 종이 "밖" 아래에 매단다. 종이 안에 두면 잘려 나간 자리에서 뒤가 아니라
        // 종이 자신의 채움이 비쳐, 오려낸 게 아니라 종이 위에 지그재그를 그린 꼴이 된다.
        // 아래변(anchor 0,0)에 톱니 윗변(pivot 0,1)을 걸어 종이 밑으로 떨어뜨린다.
        //
        // 종이 아래 두 줄에는 가로 테두리가 없다(NotePaperSprite). 그 열린 자리를
        // 톱니가 이어받아 종이의 아래 끝이 된다.
        Image torn = CreateImage("TornEdge", sheet.transform, BottomLeft,
                                 Vector2.zero, new Vector2(NoteWidth, NoteTornHeight),
                                 Color.white, NoteTornSprite());
        torn.type = Image.Type.Tiled;
        torn.pixelsPerUnitMultiplier = 1f;
        torn.raycastTarget = false;
        torn.rectTransform.pivot = TopLeft;
        torn.rectTransform.anchoredPosition = Vector2.zero;

        return new OrderNoteRefs
        {
            Dialogue = dialogue,
            Paper = sheet.rectTransform,
            Fade = noteFade,
            DayLabel = dayLabel,
            CustomerLabel = customerLabel,
        };
    }

    /// <summary>주문서 점선 한 줄. 4x1 짜리 점 한 칸을 가로로 깐다.</summary>
    private static void CreateNoteRule(Transform parent, Vector2 anchor, float y)
    {
        Image rule = CreateImage("Rule", parent, anchor, new Vector2(NoteMargin, y),
                                 new Vector2(NoteTextWidth, 1f), NoteFadeColor, NoteRuleSprite());
        rule.type = Image.Type.Tiled;
        rule.pixelsPerUnitMultiplier = 1f;
        rule.raycastTarget = false;
        rule.rectTransform.pivot = anchor;
        rule.rectTransform.anchoredPosition = new Vector2(NoteMargin, y);
    }

    // 시간·수익 판은 막대와 아이콘을 따로 잘라 쓴다.
    // 한 장으로 9-슬라이스하면 아이콘이 판 높이에 묶여 작게만 나온다.
    // 막대는 늘어나도 되는 둥근 사각형이라 마음껏 늘리고, 아이콘은 원본 비율로 크게 얹는다.

    /// <summary>
    /// 일차 판. **돈 판과 같은 그림을 쓴다.**
    ///
    /// 원래는 Time.png 의 막대를 따로 썼는데, 그쪽 테두리가 (53,37,23) 갈색이고 돈 판은
    /// (0,0,0) 검정이라 둘이 나란히 서면 한쪽만 바랜 것처럼 보였다. 9-슬라이스라
    /// 그림이 같아도 폭은 각자 잡힌다.
    /// </summary>
    private static Sprite TimeBarSprite()
    {
        return MoneyBarSprite();
    }

    /// <summary>
    /// 상단 판. 왼쪽 외곽선을 그려 넣은 것을 따로 구워 쓴다.
    ///
    /// 원본 그림은 아이콘이 판 왼쪽 끝에 겹쳐 그려져 있다. 아이콘을 빼고 막대만 자르려면
    /// 아이콘 오른쪽에서 잘라야 하는데, 그 자리가 하필 판의 왼쪽 외곽선이라 선까지 잘려 나간다.
    /// 그래서 오른쪽 변에서 테두리 색을 떠다가 왼쪽 끝에 한 줄 긋는다.
    ///
    /// 위아래 모서리 줄은 건드리지 않는다. 그 줄들은 오른쪽 끝이 테두리가 아니라 비어 있거나
    /// 그림자라, 같이 칠하면 모서리가 각지거나 색이 튄다.
    /// </summary>
    private static Sprite BarSprite(string sourcePath, string outputName, RectInt area, Vector4 border)
    {
        Texture2D texture = ReadableTexture(sourcePath);
        if (texture == null) return null;

        Color[] px = texture.GetPixels(area.x, area.y, area.width, area.height);

        for (int y = 0; y < area.height; y++)
        {
            Color right = px[y * area.width + (area.width - 1)];
            if (!IsOutlineColor(right)) continue;

            px[y * area.width] = right;
        }

        string path = SaveGenerated(outputName, EncodePng(px, area.width, area.height));
        if (path == null) return null;

        return LoadSlicedSprite(path, border);
    }

    /// <summary>
    /// 시계 아이콘의 기본 그림(0명 지남, 전부 노랑). 20x18 을 코드로 구워 Generated 에 둔다.
    ///
    /// 예전에는 Time.png 의 달(10x9)을 오려 썼다. 손님이 갈 때마다 검정 조각이 차오르는 시계로
    /// 바꾸면서, 10x9 로는 8조각이 안 갈려 두 배 해상도로 새로 그린다. 실제 칠하기는
    /// <see cref="DayClockIcon.Paint"/> 가 하고 Play 중에는 그 컴포넌트가 다시 칠한다.
    /// </summary>
    private static Sprite TimeIconSprite()
    {
        int w = DayClockIcon.Width, h = DayClockIcon.Height;
        var px32 = new Color32[w * h];
        DayClockIcon.Paint(px32, w, h, 0f);

        var px = new Color[px32.Length];
        for (int i = 0; i < px.Length; i++) px[i] = px32[i];

        string path = SaveGenerated("ClockIcon", EncodePng(px, w, h));
        return path == null ? null : LoadSprite(path);
    }

    private static Sprite MoneyBarSprite()
    {
        return BarSprite(UiDir + "Money_UI2.png", "MoneyBar",
                         new RectInt(17, 16, 40, 20), new Vector4(6f, 6f, 6f, 6f));
    }

    /// <summary>
    /// 원 표시 배지만. 그림 좌표로 x8~16, y25~31. x7 칸은 막대의 왼쪽 테두리라 뺀다.
    ///
    /// 원본은 9x7 인데 두 배(18x14)로 키운 뒤 바깥 검정을 한 겹 깎아 저장한다(ThinOutline2x).
    /// 판에는 36x28 로 얹으므로 화면에서는 2배 — 시계 아이콘(20x18 을 2배)과 같은 배율이라
    /// 검정 테두리 두께가 둘이 같아진다. 예전엔 9x7 을 4배로 띄워 돈 쪽만 테두리가 두 배였다.
    /// </summary>
    private static Sprite MoneyIconSprite()
    {
        return LoadCleanedIcon(UiDir + "Money_UI2.png", "MoneyIcon", new RectInt(8, 32, 9, 7));
    }

    /// <summary>
    /// 그림을 두 배로 키우고, 바깥(투명)에 닿은 검정을 한 겹 지운다.
    /// 모양은 그대로인데 테두리만 원본 1칸 → 두 배 그림에서 1칸이 된다.
    /// 획 사이의 검정은 투명에 닿지 않으므로 그대로 두 겹이다.
    /// </summary>
    private static Color[] ThinOutline2x(Color[] src, int w, int h)
    {
        int w2 = w * 2, h2 = h * 2;
        var big = new Color[w2 * h2];
        for (int y = 0; y < h2; y++)
            for (int x = 0; x < w2; x++)
                big[y * w2 + x] = src[(y / 2) * w + (x / 2)];

        var outPx = (Color[])big.Clone();
        for (int y = 0; y < h2; y++)
        {
            for (int x = 0; x < w2; x++)
            {
                Color c = big[y * w2 + x];
                bool black = c.a > 0.1f && c.r < 0.45f && c.g < 0.45f && c.b < 0.45f;
                if (!black) continue;

                bool touchesOutside = IsClear(big, w2, h2, x + 1, y) || IsClear(big, w2, h2, x - 1, y)
                                   || IsClear(big, w2, h2, x, y + 1) || IsClear(big, w2, h2, x, y - 1);
                if (touchesOutside) outPx[y * w2 + x] = new Color(0f, 0f, 0f, 0f);
            }
        }
        return outPx;
    }

    private static bool IsClear(Color[] px, int w, int h, int x, int y)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return true;
        return px[y * w + x].a <= 0.1f;
    }

    /// <summary>
    /// 판 그림에서 아이콘만 오려 내고, 뒤에 비치는 흰 막대를 지운 그림을 따로 만든다.
    ///
    /// 아이콘과 막대가 서로 물려 그려져 있어 사각형으로 자르면 흰 픽셀이 같이 딸려 온다.
    /// 아이콘을 막대 밖에 놓으면 그 흰색이 배경 위에 덩어리로 드러난다.
    /// 그래서 잘라낸 뒤 흰색만 투명으로 바꿔 Generated 폴더에 저장해 두고 그걸 쓴다.
    /// </summary>
    private static Sprite LoadCleanedIcon(string sourcePath, string outputName, RectInt area,
                                          bool inkToBlack = false)
    {
        string folder = UiDir + "Generated";
        string outPath = folder + "/" + outputName + ".png";

        byte[] made = BuildCleanedIconBytes(sourcePath, area, inkToBlack);
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
    private static byte[] BuildCleanedIconBytes(string sourcePath, RectInt area, bool inkToBlack)
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
            if (white) { pixels[i] = new Color(0f, 0f, 0f, 0f); continue; }

            // 어두운 획을 새까맣게 맞춘다.
            //
            // 시계 아이콘의 테두리는 (53,37,23) 갈색이고 돈 아이콘은 (0,0,0) 검정이다.
            // 두 판이 나란히 서는데 테두리 색이 달라 한쪽만 바랜 것처럼 보였다.
            if (inkToBlack && c.a > 0.1f && c.r < 0.45f && c.g < 0.45f && c.b < 0.45f)
            {
                pixels[i] = new Color(0f, 0f, 0f, c.a);
            }
        }

        // 시계 아이콘과 테두리 두께를 맞추려고 두 배로 키우고 바깥 검정을 한 겹 깎는다.
        pixels = ThinOutline2x(pixels, area.width, area.height);

        var cut = new Texture2D(area.width * 2, area.height * 2, TextureFormat.RGBA32, false);
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
    private static Image AttachPanelIcon(Image panel, Sprite icon, Vector2 size)
    {
        if (icon == null) return null;

        // 아이콘 가운데를 판의 왼쪽 외곽선에 얹는다. 반은 판 안으로, 반은 밖으로 걸친다.
        // 예전에는 아이콘을 판 왼쪽 바깥에 세워 3칸만 물렸다. 판마다 아이콘 너비가 달라
        // 걸치는 정도가 제각각으로 보였다. 이제 너비와 무관하게 외곽선이 늘 한가운데다.
        //
        // 반칸이 남으면 아이콘 전체가 픽셀 격자에서 반 칸 밀린다. 정수로 끊는다.
        float x = Mathf.Round(-panel.rectTransform.sizeDelta.x * 0.5f);

        Image image = CreateImage("Icon", panel.transform, Center, new Vector2(x, 0f), size, Color.white, icon);
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
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
    /// <summary>
    /// 리뉴얼된 손님 그림을 전부 한 줄로 모은다.
    ///
    /// 파일 이름이 곧 personaId 다(Polite.png → Polite). 잘린 스프라이트는 "Polite_0" 처럼
    /// 이름이 붙으므로, 런타임에서 앞머리만 보고 어느 손님 것인지 가를 수 있다.
    /// 장난꾸러기(Joker)는 그림이 없다 — 그 말투는 대사 DB 에서 빠져 있다.
    /// </summary>
    /// <summary>
    /// 인영에서 "그림이 있다"로 칠 알파. 이보다 옅은 가장자리는 잘라 낸다.
    /// </summary>
    private const float SilhouetteAlpha = 0.35f;

    /// <summary>
    /// 걸어 들어올 때 쓰는 인영. 손님 그림 첫 장의 실루엣을 안쪽까지 메워 흰색 한 장으로 굽는다.
    ///
    /// 원본을 그대로 까맣게 칠하면 그림 안쪽의 빈 자리(팔과 몸 사이, 안경알 같은 곳)로 배경이
    /// 비쳐 사람 몸에 구멍이 뽁뽁 뚫린 것처럼 보인다. 열넷 중 넷이 그런 자리를 갖고 있다.
    ///
    /// 바깥 가장자리에서 빈 자리를 타고 흘려 넣어(flood fill) "바깥"을 먼저 찾는다.
    /// 거기에 닿지 않은 빈 자리가 곧 안쪽 구멍이라, 그것만 메우면 실루엣은 그대로 두고
    /// 몸에 뚫린 자리만 막힌다.
    ///
    /// 흰색으로 굽고 색은 CustomerAppearance.SetTint 가 입힌다. 다른 그림들과 같은 방식이다.
    /// </summary>
    private static Sprite[] LoadCustomerSilhouettes()
    {
        string folder = "Assets/Art/손님/";
        var all = new System.Collections.Generic.List<Sprite>();

        foreach (string id in CustomerPersonaIds)
        {
            string path = folder + id + ".png";
            if (!System.IO.File.Exists(path)) continue;

            Sprite made = SilhouetteSprite(path, id, (int)CustomerPortraitSize);
            if (made != null) all.Add(made);
        }

        return all.ToArray();
    }

    /// <summary>
    /// 주문 결과창 대사 옆에 붙는 손님 초상. 파이썬이 미리 구워 둔 것을 읽기만 한다
    /// (Tools/make_customer_thumbs.py). 스프라이트 이름이 곧 말투라 따로 붙일 것이 없다.
    ///
    /// 인영처럼 여기서 굽지 않는 까닭 — 얼굴을 48칸으로 줄이려면 주변 픽셀을 섞어야 하는데,
    /// 그 일은 파이썬(LANCZOS)이 훨씬 잘한다. 유니티에서 줄이면 픽셀을 골라 쓰고 버려서
    /// 눈 한 줄이 통째로 사라진다.
    /// </summary>
    private static Sprite[] LoadCustomerFaces()
    {
        string folder = GeneratedDir + "손님얼굴/";
        var all = new System.Collections.Generic.List<Sprite>();

        foreach (string id in CustomerPersonaIds)
        {
            string path = folder + id + ".png";
            if (!System.IO.File.Exists(path)) continue;

            Sprite found = LoadSprite(path);
            if (found != null) all.Add(found);
        }

        return all.ToArray();
    }

    /// <summary>손님 그림 한 장에서 인영을 뜬다. 첫 칸만 쓴다 — 걷는 동안에는 깜빡이지 않는다.</summary>
    private static Sprite SilhouetteSprite(string sourcePath, string id, int width)
    {
        Texture2D texture = ReadableTexture(sourcePath);
        if (texture == null) return null;

        int w = Mathf.Min(width, texture.width);
        int h = texture.height;
        if (w <= 0 || h <= 0) return null;

        Color[] src = texture.GetPixels(0, 0, w, h);

        var solid = new bool[w * h];
        for (int i = 0; i < solid.Length; i++) solid[i] = src[i].a > SilhouetteAlpha;

        var outside = new bool[w * h];
        var todo = new System.Collections.Generic.Stack<int>();

        System.Action<int, int> spill = (x, y) =>
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;

            int i = y * w + x;
            if (solid[i] || outside[i]) return;

            outside[i] = true;
            todo.Push(i);
        };

        for (int x = 0; x < w; x++) { spill(x, 0); spill(x, h - 1); }
        for (int y = 0; y < h; y++) { spill(0, y); spill(w - 1, y); }

        while (todo.Count > 0)
        {
            int i = todo.Pop();
            int x = i % w;
            int y = i / w;

            spill(x - 1, y);
            spill(x + 1, y);
            spill(x, y - 1);
            spill(x, y + 1);
        }

        var made = new Color[w * h];
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int i = 0; i < made.Length; i++) made[i] = outside[i] ? clear : Color.white;

        string path = SaveGenerated("Silhouette_" + id, EncodePng(made, w, h));
        return path == null ? null : LoadSprite(path);
    }

    private static Sprite[] LoadCustomerPortraits()
    {
        string folder = "Assets/Art/손님/";
        var all = new System.Collections.Generic.List<Sprite>();

        foreach (string id in CustomerPersonaIds)
        {
            string path = folder + id + ".png";
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning("[RamenLayoutBuilder] 손님 그림이 없습니다: " + path);
                continue;
            }

            // 손님마다 키가 달라 시트 높이가 다르다. 머리 위 빈 줄을 잘라 냈기 때문인데,
            // 덕분에 스프라이트 높이가 곧 머리 꼭대기가 된다(말풍선을 그 위에 올린다).
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) continue;

            all.AddRange(LoadSpriteSheet(path, (int)CustomerPortraitSize, texture.height));
        }

        if (all.Count == 0)
            Debug.LogWarning("[RamenLayoutBuilder] 손님 그림을 하나도 못 찾았습니다: " + folder);

        return all.ToArray();
    }

    /// <summary>그림이 있는 말투. 대사 DB 의 personaId 와 글자까지 같아야 한다.</summary>
    private static readonly string[] CustomerPersonaIds =
    {
        "Polite", "Formal", "Gyeongsang", "Chungcheong", "Jeolla", "Otaku", "Military",
        "Sageuk", "Grandma", "Grandpa", "Youtuber", "Gourmet", "Emotional", "Child"
    };

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

    /// <summary>
    /// 말풍선 본문. 꼬리를 뺀 사각형만 9-슬라이스로 쓴다.
    ///
    /// 원본을 그대로 잘라 쓰지 않고 구워서 쓴다. 오른쪽 테두리에 뚫린 꼬리 입구를 메워야
    /// 하기 때문이다. 까닭은 BuildBubbleBodyBytes 에 적었다.
    /// </summary>
    private static Sprite SpeechBubbleBodySprite()
    {
        string path = SaveGenerated("BubbleBody", BuildBubbleBodyBytes());
        if (path == null) return null;

        return LoadSlicedSprite(path, new Vector4(4f, 4f, 4f, 4f));
    }

    /// <summary>
    /// 말풍선 꼬리. 본문 오른쪽에 따로 붙인다.
    ///
    /// 꼬리까지 9-슬라이스에 넣으면 세로로 늘어나 뭉개진다. 그렇다고 원본 8x5 를 그대로 두면
    /// 240x96 짜리 말풍선에서 눈에 안 띈다. 실루엣만 키워 다시 구운 그림을 쓴다.
    /// </summary>
    private static Sprite SpeechBubbleTailSprite()
    {
        string path = SaveGenerated("BubbleTail", BuildBubbleTailBytes());
        if (path == null) return null;

        return LoadSprite(path);
    }

    /// <summary>
    /// 본문 PNG. 원본에서 잘라 오되 오른쪽 테두리에 뚫린 꼬리 입구를 메운다.
    ///
    /// 원본에는 꼬리가 붙는 자리의 테두리가 세 줄 터져 있다. 9-슬라이스는 가운데를 늘리는데
    /// 하필 그 세 줄이 늘어나는 구간에 들어가, 28칸을 96칸으로 키우면 4.4배가 되어
    /// 13칸짜리 구멍이 됐다. 꼬리는 위에서 잰 거리로 따로 고정돼 있어 구멍과 만나지도 않는다.
    ///
    /// 그래서 테두리는 끊김 없이 메워 두고, 입은 꼬리 그림이 본문 테두리를 한 칸 물고
    /// 덮어서 만든다.
    /// </summary>
    private static byte[] BuildBubbleBodyBytes()
    {
        Texture2D texture = ReadableTexture(UiDir + "Order UI.png");
        if (texture == null) return null;

        Color[] px = texture.GetPixels(BubbleBodyX, BubbleBodyY, BubbleBodyW, BubbleBodyH);

        int clean = CleanEdgeRow(px);
        if (clean < 0) return null;

        for (int y = 0; y < BubbleBodyH; y++)
        {
            Color right = px[y * BubbleBodyW + (BubbleBodyW - 1)];

            // 위아래 둥근 모서리는 오른쪽 끝이 비어 있다. 거기까지 메우면 모서리가 각진다.
            if (right.a <= 0.5f || IsOutlineColor(right)) continue;

            for (int i = 1; i <= 4; i++)
                px[y * BubbleBodyW + (BubbleBodyW - i)] = px[clean * BubbleBodyW + (BubbleBodyW - i)];
        }

        return EncodePng(px, BubbleBodyW, BubbleBodyH);
    }

    /// <summary>
    /// 꼬리 PNG. 원본 실루엣만 정수배로 키우고 외곽선을 1픽셀로 다시 긋는다.
    ///
    /// 그림을 통째로 늘리면 1픽셀이던 외곽선도 같이 굵어져 본문 테두리와 따로 논다.
    /// 본문에 물리는 왼쪽 변은 테두리를 치지 않는다. 그 열린 자리가 꼬리의 입이다.
    /// </summary>
    private static byte[] BuildBubbleTailBytes()
    {
        Texture2D texture = ReadableTexture(UiDir + "Order UI.png");
        if (texture == null) return null;

        if (!BubblePalette(texture, out Color outline, out Color fill, out Color shade)) return null;

        int n = Mathf.Max(1, BubbleTailScale);
        int w = BubbleTailW * n;
        int h = BubbleTailH * n;

        // 원본 8x5 를 그대로 확대하지 않는다. 확대하면 비스듬한 변이 세 칸씩 뚝뚝 끊겨
        // 계단이 굵게 보인다. 같은 모양을 제 크기에서 다시 그려 계단을 한 칸씩으로 만든다.
        //
        // 모양은 원본을 따른다. 위쪽 절반쯤은 폭이 그대로고 거기서부터 아래로 좁아진다.
        // 원본의 줄별 폭이 8·8·8·6·3 이라 위 60% 가 평평하고 아래가 빠르게 준다.
        int flatRows = Mathf.RoundToInt(h * 0.45f);
        float endWidth = w * 0.3f;

        var solid = new bool[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = h - 1 - flatRows <= 0 ? 1f : (y - flatRows) / (float)(h - 1 - flatRows);
            float rowWidth = y < flatRows ? w : Mathf.Lerp(w, endWidth, Mathf.Clamp01(t));
            int cut = Mathf.Max(1, Mathf.RoundToInt(rowWidth));

            // SetPixels 는 아래에서 위로 담는다. 위에서부터 그린 줄을 뒤집어 넣어야
            // 원본처럼 위가 넓고 아래로 좁아진다.
            for (int x = 0; x < cut; x++) solid[(h - 1 - y) * w + x] = true;
        }

        var kind = new byte[w * h];                  // 0 빈칸 · 1 테두리 · 2 채움 · 3 그림자
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!solid[y * w + x]) continue;

                bool edge = !(TailInside(solid, w, h, x - 1, y) && TailInside(solid, w, h, x + 1, y)
                              && TailInside(solid, w, h, x, y - 1) && TailInside(solid, w, h, x, y + 1));
                kind[y * w + x] = (byte)(edge ? 1 : 2);
            }
        }

        // 아래쪽 테두리 바로 안에 그림자를 한 줄 넣는다.
        // GetPixels 는 아래에서 위로 담기므로 '아래'가 y-1 이다.
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (kind[y * w + x] != 2) continue;

                byte below = y == 0 ? (byte)0 : kind[(y - 1) * w + x];
                if (below == 0 || below == 1) kind[y * w + x] = 3;
            }
        }

        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++)
        {
            px[i] = kind[i] == 1 ? outline
                  : kind[i] == 2 ? fill
                  : kind[i] == 3 ? shade
                  : new Color(0f, 0f, 0f, 0f);
        }

        return EncodePng(px, w, h);
    }

    /// <summary>
    /// 꼬리 실루엣 안쪽인가. 왼쪽 바깥은 본문이 이어진다고 본다.
    /// 그래야 본문에 물리는 변에 외곽선이 생기지 않는다.
    /// </summary>
    private static bool TailInside(bool[] solid, int w, int h, int x, int y)
    {
        if (x < 0) return true;

        return x < w && y >= 0 && y < h && solid[y * w + x];
    }

    /// <summary>
    /// 말풍선 원본의 세 가지 색. 테두리가 온전한 줄의 오른쪽 끝에서 그대로 뜬다.
    /// 값을 코드에 적어 두면 그림을 다시 칠했을 때 꼬리만 옛 색으로 남는다.
    /// </summary>
    private static bool BubblePalette(Texture2D texture, out Color outline, out Color fill, out Color shade)
    {
        outline = fill = shade = Color.clear;

        Color[] px = texture.GetPixels(BubbleBodyX, BubbleBodyY, BubbleBodyW, BubbleBodyH);

        int clean = CleanEdgeRow(px);
        if (clean < 0) return false;

        int right = clean * BubbleBodyW + (BubbleBodyW - 1);
        outline = px[right];
        shade = px[right - 1];
        fill = px[right - 2];
        return true;
    }

    /// <summary>
    /// 오른쪽 테두리가 온전한 줄. 검은 칸이 하나뿐인 줄을 찾는다.
    /// 둥근 모서리 줄은 검은 칸이 둘이라 거기서 뜨면 메운 자리만 테두리가 굵어진다.
    /// </summary>
    private static int CleanEdgeRow(Color[] px)
    {
        for (int y = 4; y < BubbleBodyH - 4; y++)
        {
            int right = y * BubbleBodyW + (BubbleBodyW - 1);
            if (IsOutlineColor(px[right]) && !IsOutlineColor(px[right - 1])) return y;
        }

        Debug.LogWarning("[RamenLayoutBuilder] 말풍선 오른쪽 테두리가 온전한 줄을 찾지 못했습니다.");
        return -1;
    }

    /// <summary>말풍선 검은 테두리인가. 채움·그림자와 구별만 되면 된다.</summary>
    private static bool IsOutlineColor(Color c)
    {
        return c.a > 0.5f && c.r + c.g + c.b < 0.9f;
    }

    /// <summary>픽셀을 읽을 수 있게 만든 뒤 그림을 돌려준다.</summary>
    private static Texture2D ReadableTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] 그림을 찾지 못했습니다: " + path);
            return null;
        }

        if (!importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>픽셀 배열을 PNG 바이트로 굽는다.</summary>
    private static byte[] EncodePng(Color[] pixels, int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();

        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);
        return png;
    }

    /// <summary>구운 PNG 를 Generated 폴더에 둔다. 내용이 같으면 다시 쓰지 않는다.</summary>
    private static string SaveGenerated(string outputName, byte[] made)
    {
        if (made == null) return null;

        string folder = UiDir + "Generated";
        string outPath = folder + "/" + outputName + ".png";

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

        return outPath;
    }

    /// <summary>튜토리얼 테두리 두께(원본 픽셀).</summary>
    private const int TutorialOutlineThickness = 2;


    // 튜토리얼 어두운 판과 그 위로 올라오는 것들의 그리기 순서.
    // 판보다 큰 값이어야 판 위에 그려진다.
    private const int TutorialDimOrder = 100;
    private const int TutorialLiftOrder = 101;

    /// <summary>
    /// 튜토리얼 안내 판. 어두운 판(100)과 올라온 통(101)보다 앞이다.
    /// 무엇을 하라는 글이 어두워지면 읽을 수가 없다.
    /// </summary>
    private const int TutorialPromptOrder = 110;

    /// <summary>
    /// 커서(젓가락·국자)와 드래그 고스트.
    ///
    /// 계층에서 맨 뒤에 만들어도 어두운 판은 자기 Canvas(100)로 그 위에 그려진다. 그래서
    /// 튜토리얼이 도는 동안 젓가락이 판 밑에 깔렸다. 재료통 위에서는 시스템 커서가 이미
    /// 꺼진 뒤라, 화면에서 커서가 통째로 사라진 것처럼 보였다.
    ///
    /// 주문서(150)보다는 뒤에 둔다. 예전 그리기 순서를 그대로 지키려는 것이다.
    /// </summary>
    private const int DragLayerOrder = 120;

    /// <summary>
    /// Tab 주문서와 B 레시피북. 재료를 옮기는 동안에는 그릇이 어두운 판 위로 올라오므로(101),
    /// 그냥 두면 펼친 책이 그릇 뒤로 들어간다.
    /// </summary>
    private const int OverlayPanelOrder = 150;

    /// <summary>
    /// 주문 화면. 조리 화면의 모든 것보다 앞이어야 한다.
    ///
    /// 그릇과 재료통이 튜토리얼 때문에 자기 Canvas 로 앞에 나와 있어서(101), 그냥 두면
    /// 주문 화면이 미끄러져 올라오는 동안 그릇만 그 위에 떠 있다.
    /// 판이 한 덩어리로 움직이려면 주문 화면이 그것들보다 앞이어야 한다.
    /// </summary>
    private const int OrderScreenOrder = 180;

    /// <summary>
    /// 결과창·정산창처럼 주문 화면 위에 떠야 하는 팝업.
    ///
    /// 손님별 결과창은 먹는 화면 위에 뜬다. 주문 화면이 자기 Canvas(180)를 갖게 된 뒤로는
    /// 계층에서 뒤에 놓는 것만으로는 덮을 수 없다 — 실제로 결과창이 통째로 가려져 있었다.
    /// </summary>
    private const int PopupOrder = 185;

    /// <summary>시작 화면. 튜토리얼 어두운 판보다도 앞이어야 한다.</summary>
    private const int TitleOrder = 200;

    /// <summary>
    /// 설정창. **시작 화면과 인게임이 같은 창을 쓴다.**
    ///
    /// 그래서 시작 화면(200) 밑에 두면 안 된다 — 인게임에서 시작 화면이 꺼지면 설정창까지
    /// 같이 사라진다. 캔버스 바로 밑에 따로 세우고 시작 화면보다 앞에 그린다.
    ///
    /// 전환 판(300)보다는 뒤다. 화면이 넘어가는 중에는 설정창도 같이 덮여야 한다.
    /// </summary>
    private const int SettingsOrder = 210;

    // 얇은면 바구니 자리. 통 그림 첫 칸을 줄 단위로 훑어 잰 값이다.
    // 바구니는 x 26~59 · y 59~79, 주황 손잡이는 x 20~32 · y 85~100 이었다.
    // 통 그림을 바꾸면 다시 재야 한다.
    private const int BasketSeedX = 42;
    private const int BasketSeedY = 66;
    private const int GripFromY = 82;
    private const int BasketFillFromY = 55;
    private const int BasketFillToY = 82;
    private const int RodRadius = 3;

    // ── 화면 전환 · 키 안내 ───────────────────────────────────────

    /// <summary>전환 판. 시작 화면(200)보다 앞이되 그 위에서 덮는다.</summary>
    private const int ScreenFadeOrder = 300;

    private static void BuildScreenFade(Transform canvas)
    {
        Transform root = CreateGroup("ScreenFade", canvas);

        var own = Undo.AddComponent<Canvas>(root.gameObject);
        own.overrideSorting = true;
        own.sortingOrder = ScreenFadeOrder;
        Undo.AddComponent<GraphicRaycaster>(root.gameObject);

        Image cover = CreateImage("Cover", root, Center, Vector2.zero, ScreenCover,
                                  new Color(0f, 0f, 0f, 0f));
        cover.enabled = false;

        var fade = Undo.AddComponent<ScreenFade>(root.gameObject);
        SetPrivateReference(fade, "cover", cover);
    }

    // ── 도입부 내레이션 ───────────────────────────────────────────
    //
    // 스토리 모드를 고르면 검은 화면에서 먼저 돈다. 줄은 위에서부터 쌓이고,
    // 한 줄이 끝나면 그 밑에 ▼ 가 깜빡인다.

    /// <summary>내레이션. 전환 판(300)보다 앞이어야 검게 덮인 위에 글자가 보인다.</summary>
    private const int NarrationOrder = 310;

    /// <summary>글 덩이의 윗변. 화면 위쪽에서 시작해 아래로 쌓인다.</summary>
    private const float NarrationTopY = 170f;

    private static readonly Vector2 NarrationBlockSize = new Vector2(560f, 300f);

    /// <summary>
    /// ▼ 크기. 구운 그림이 9x5 라 정수배인 두 배로 띄운다.
    /// 소수배로 늘리면 삼각형 빗변이 반칸에 걸려 가장자리가 지저분해진다.
    /// </summary>
    private static readonly Vector2 NarrationPromptSize = new Vector2(18f, 10f);

    /// <summary>하루 바뀜 글자. 내레이션(310)과 같은 층대, 아이리스(320)보다는 뒤다.</summary>
    private const int DayTitleOrder = 315;

    /// <summary>튜토리얼 물음판. 자막(315)과 같은 층대, 아이리스(320)보다는 뒤다.</summary>
    private const int TutorialAskOrder = 316;

    /// <summary>
    /// 「튜토리얼을 보시겠습니까?」. 도입부 내레이션과 「N일차」 자막 사이, 검은 화면에 이 판만 뜬다.
    ///
    /// 뒷판을 깔지 않는다. 확인창(ConfirmDialogUI)은 조리 화면 위에 뜨느라 어두운 판이 필요하지만,
    /// 여기는 이미 검은 화면이라 한 겹 더 깔면 내레이션 판과 겹쳐 두 번 어두워진다.
    ///
    /// 판·버튼 그림은 폐기·마무리 확인창과 **같은 것**을 쓴다. 게임에서 묻는 창은 다 같은 결이어야 한다.
    /// </summary>
    private static void BuildTutorialAsk(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("TutorialAsk", canvas);
        var own = Undo.AddComponent<Canvas>(root.gameObject);
        own.overrideSorting = true;
        own.sortingOrder = TutorialAskOrder;
        Undo.AddComponent<GraphicRaycaster>(root.gameObject);

        // 껐다 켜는 것은 이 안쪽이다. 스크립트가 붙은 root 를 끄면 코루틴이 같이 멈춘다.
        Transform panelRoot = CreateGroup("Panel", root);
        var group = Undo.AddComponent<CanvasGroup>(panelRoot.gameObject);

        // 확인창은 300 인데 여기는 **324** 다. 「튜토리얼을 보시겠습니까?」가 24픽셀 글자로
        // 286칸이라 280 상자에서 「까?」가 다음 줄로 넘어갔다. 폐기 창(262)보다 한 마디 길다.
        Image panel = CreateImage("Box", panelRoot, Center, Vector2.zero, new Vector2(324f, 120f),
                                  Hex("#FFF8E7"), DialogBoxSprite());
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 1f;

        CreateTmpText("MessageText", panel.transform, Center, new Vector2(0f, 22f),
                      new Vector2(304f, 27f), "튜토리얼을 보시겠습니까?", TextTitle, tmpFont);

        // 확인창과 같은 자리·같은 색이다. 「넵」이 붉고 「아뇨」가 푸르다.
        Button yes = MakeDialogButton("YesButton", panel.transform, new Vector2(-56f, -26f),
                                      "넵", Hex("#C05A4A"), tmpFont);
        Button no = MakeDialogButton("NoButton", panel.transform, new Vector2(56f, -26f),
                                     "아뇨", Hex("#7BA7C7"), tmpFont);

        var ask = Undo.AddComponent<TutorialAskUI>(root.gameObject);
        SetPrivateReference(ask, "root", panelRoot.gameObject);
        SetPrivateReference(ask, "group", group);
        SetPrivateReference(ask, "yesButton", yes);
        SetPrivateReference(ask, "noButton", no);

        panelRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 하루가 바뀔 때 검은 화면에 뜨는 「N일차」.
    ///
    /// 검은 판은 안 만든다. 화면을 덮는 것은 ScreenFade(300)가 하고 이 글자는 그 위에 얹힌다.
    /// 여기서 판을 또 들면 검은 것이 두 겹이라 걷히는 박자가 어긋난다.
    /// </summary>
    private static void BuildDayTitle(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("DayTitle", canvas);
        var own = Undo.AddComponent<Canvas>(root.gameObject);
        own.overrideSorting = true;
        own.sortingOrder = DayTitleOrder;

        // 껐다 켜는 것은 이 안쪽이다. 스크립트가 붙은 root 를 끄면 코루틴이 같이 멈춘다.
        Transform panel = CreateGroup("Panel", root);
        var group = Undo.AddComponent<CanvasGroup>(panel.gameObject);
        group.blocksRaycasts = false;
        group.interactable = false;

        // 두 줄이다 — 「N일차」 밑에 「목표 35,000원」. 24 두 줄이 60칸이라 40 으로는 아랫줄이 잘렸다.
        // 줄 사이를 벌려 72 가 되므로 상자는 84 로 여유를 준다.
        var label = CreateTmpText("DayText", panel, Center, Vector2.zero,
                                  new Vector2(400f, 84f), "2일차\n목표 35,000원", TextTitle, tmpFont);

        // 두 줄이 붙어 있으면 한 덩이로 읽힌다. 50 이면 24 글자에서 딱 12픽셀 벌어진다 —
        // 2배 배율이라 원본 6픽셀이고 정수라서 픽셀 격자가 안 깨진다.
        label.lineSpacing = 50f;

        // 세로는 **위 정렬**이고 피벗도 위다. 가운데 정렬로 두면 둘째 줄이 나타나는 순간
        // 글 덩이가 다시 가운데를 잡으면서 「N일차」가 위로 밀려 올라간다.
        // 위에 박아 두면 첫 줄은 끝까지 제자리에 있고 둘째 줄만 아래에 붙는다.
        label.alignment = TextAlignmentOptions.Top;
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.rectTransform.anchoredPosition = new Vector2(0f, 42f);

        label.raycastTarget = false;
        label.color = Color.white;

        var title = Undo.AddComponent<DayTitleUI>(root.gameObject);
        SetPrivateReference(title, "root", panel.gameObject);
        SetPrivateReference(title, "label", label);
        SetPrivateReference(title, "group", group);

        // 글자가 한 자씩 찍히는 톤. 내레이션·손님 대사와 같은 것을 나눠 쓴다.
        SetPrivateReference(title, "blip", Object.FindFirstObjectByType<DialogueBlip>());

        panel.gameObject.SetActive(false);
    }

    private static void BuildOpeningNarration(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("OpeningNarration", canvas);

        var own = Undo.AddComponent<Canvas>(root.gameObject);
        own.overrideSorting = true;
        own.sortingOrder = NarrationOrder;
        Undo.AddComponent<GraphicRaycaster>(root.gameObject);

        // 껐다 켜는 것은 이 안쪽이다. 스크립트가 붙은 root 를 끄면 코루틴이 같이 멈춘다.
        Transform panel = CreateGroup("Panel", root);

        // 자기 검은 판을 든다. 전환 판이 걷혀도 글자 뒤는 검어야 한다.
        Image backdrop = CreateImage("Backdrop", panel, Center, Vector2.zero, ScreenCover, Color.black);
        backdrop.raycastTarget = true;

        // 검은 화면에 이 글만 있다. 본문 크기로는 허전해서 제목 크기를 쓴다.
        var label = CreateTmpText("LineText", panel, Center, new Vector2(0f, NarrationTopY),
                                  NarrationBlockSize, string.Empty, TextTitle, tmpFont);

        // 피벗을 위로 올린다. 그래야 줄이 늘어도 윗변이 제자리에 있고 아래로만 자란다.
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.rectTransform.anchoredPosition = new Vector2(0f, NarrationTopY);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.color = Color.white;

        // 줄 사이를 벌린다. 24 글자에서 50 이면 딱 12픽셀 — 2배 배율이라 원본 6픽셀이고
        // 정수라서 픽셀 격자가 안 깨진다. 하루 자막(DayTitle)과 같은 값이다.
        // 다섯 줄이라도 5x32 + 4x12 = 208 로 블록(300)에 들어간다.
        label.lineSpacing = 50f;

        Image prompt = CreateImage("Prompt", panel, Center, Vector2.zero, NarrationPromptSize,
                                   Color.white, TriangleSprite());
        prompt.rectTransform.pivot = new Vector2(0.5f, 1f);
        prompt.raycastTarget = false;
        prompt.enabled = false;

        var narration = Undo.AddComponent<OpeningNarration>(root.gameObject);

        // 글자 톤. 손님 대사와 같은 것을 나눠 쓴다 — 주문 시스템이 먼저 만들어져 있다.
        SetPrivateReference(narration, "blip", Object.FindFirstObjectByType<DialogueBlip>());
        SetPrivateReference(narration, "root", panel.gameObject);
        SetPrivateReference(narration, "label", label);
        SetPrivateReference(narration, "promptRect", prompt.rectTransform);
        SetPrivateReference(narration, "prompt", prompt);

        panel.gameObject.SetActive(false);
    }

    /// <summary>
    /// 따봉 뒤에서 도는 아우라. 가운데에서 뻗어 나가는 빛살 열두 가닥이다.
    ///
    /// 가운데는 비워 둔다. 따봉이 거기에 앉는데 빛살이 깔려 있으면 손 모양이 안 읽힌다.
    /// 바깥으로 갈수록 옅어져서 가장자리가 툭 끊기지 않는다.
    ///
    /// 이 그림만은 회전해도 된다. 방사형이라 돌아간 가장자리가 빛살처럼 읽히기 때문이다.
    /// 다른 픽셀아트는 회전하면 윤곽이 부서진다.
    /// </summary>
    private static Sprite AuraSprite()
    {
        const int N = 96;
        const int Rays = 12;
        const float Inner = 0.25f;

        var made = new Color[N * N];
        var clear = new Color(0f, 0f, 0f, 0f);
        float half = N * 0.5f;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                made[y * N + x] = clear;

                float dx = x + 0.5f - half;
                float dy = y + 0.5f - half;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / half;

                if (r < Inner || r > 1f) continue;

                // 각도를 빛살 수로 나눠 절반만 채운다. 톱니처럼 번갈아 비고 찬다.
                float angle = Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f;
                if (Mathf.Repeat(angle * Rays, 1f) > 0.5f) continue;

                float fade = 1f - Mathf.InverseLerp(Inner, 1f, r);
                made[y * N + x] = new Color(1f, 1f, 1f, fade * 0.75f);
            }
        }

        string path = SaveGenerated("ThumbAura", EncodePng(made, N, N));
        return path == null ? null : LoadSprite(path);
    }

    /// <summary>
    /// 감동 오우라 뒤에 까는 빛. 가운데가 밝고 바깥으로 갈수록 옅어진다.
    ///
    /// 알파를 여섯 단으로 끊는다. 매끄러운 그라데이션을 네 배로 늘리면 띠가 지저분하게
    /// 드러나는데, 처음부터 단으로 끊어 두면 그 띠가 의도한 고리로 읽힌다.
    /// </summary>
    private static Sprite RadialGlowSprite()
    {
        const int N = 160;
        const int Steps = 6;

        var made = new Color[N * N];
        var clear = new Color(0f, 0f, 0f, 0f);
        float half = N * 0.5f;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = x + 0.5f - half;
                float dy = y + 0.5f - half;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / half;

                if (r >= 1f) { made[y * N + x] = clear; continue; }

                int step = Mathf.FloorToInt((1f - r) * Steps);
                made[y * N + x] = new Color(1f, 1f, 1f, (step + 1) / (float)Steps * 0.55f);
            }
        }

        string path = SaveGenerated("AuraGlow", EncodePng(made, N, N));
        return path == null ? null : LoadSprite(path);
    }

    /// <summary>아래를 가리키는 작은 삼각형. 맨 윗줄이 제일 넓고 한 줄씩 좁아진다.</summary>
    private static Sprite TriangleSprite()
    {
        const int W = 9;
        const int H = 5;

        var made = new Color[W * H];
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int i = 0; i < made.Length; i++) made[i] = clear;

        for (int row = 0; row < H; row++)
        {
            // 그림은 아래에서 위로 쌓이므로 첫 줄이 제일 높은 y 다.
            int y = H - 1 - row;
            for (int x = row; x < W - row; x++) made[y * W + x] = Color.white;
        }

        string path = SaveGenerated("PromptArrow", EncodePng(made, W, H));
        return path == null ? null : LoadSprite(path);
    }

    // ── 아이리스 전환 ─────────────────────────────────────────────
    //
    // 가운데부터 바깥으로 밝아진다. 구멍 뚫린 판 한 장과 그 바깥을 메우는 띠 넷으로 만든다.

    /// <summary>아이리스. 내레이션(310)까지 덮어야 넘겨받는 순간이 안 보인다.</summary>
    private const int IrisOrder = 320;

    /// <summary>
    /// 바깥을 메우는 띠 하나의 크기. 화면(960x540)보다 한참 커야 판이 작을 때도 바깥이 남지 않는다.
    /// </summary>
    private static readonly Vector2 IrisBarSize = new Vector2(4000f, 4000f);

    private static void BuildIrisFade(Transform canvas)
    {
        Transform root = CreateGroup("IrisFade", canvas);

        var own = Undo.AddComponent<Canvas>(root.gameObject);
        own.overrideSorting = true;
        own.sortingOrder = IrisOrder;
        Undo.AddComponent<GraphicRaycaster>(root.gameObject);

        Transform panel = CreateGroup("Panel", root);

        // 구멍 뚫린 판. 크기는 IrisFade 가 매 프레임 정한다.
        Image hole = CreateImage("Hole", panel, Center, Vector2.zero, Vector2.zero,
                                 Color.black, IrisSprite());
        hole.raycastTarget = false;

        // 띠 넷. 피벗을 안쪽 변에 두어 자리만 옮기면 바깥이 덮인다.
        string[] names = { "BarTop", "BarBottom", "BarLeft", "BarRight" };
        var pivots = new[]
        {
            new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f)
        };

        var bars = new RectTransform[names.Length];

        for (int i = 0; i < names.Length; i++)
        {
            Image bar = CreateImage(names[i], panel, Center, Vector2.zero, IrisBarSize, Color.black);
            bar.rectTransform.pivot = pivots[i];

            // 전환이 도는 동안 뒤쪽 버튼이 눌리지 않게 막는다.
            bar.raycastTarget = true;
            bars[i] = bar.rectTransform;
        }

        var iris = Undo.AddComponent<IrisFade>(root.gameObject);
        SetPrivateReference(iris, "root", panel.gameObject);
        SetPrivateReference(iris, "hole", hole.rectTransform);
        SetPrivateArray(iris, "bars", bars);

        panel.gameObject.SetActive(false);
    }

    /// <summary>
    /// 가운데가 동그랗게 뚫린 네모 판.
    ///
    /// 구멍 반지름을 판 절반의 절반으로 잡는다 — IrisFade 의 holeToHalf 2 가 이 값이다.
    /// 가장자리는 한 칸 남짓 흐린다. 화면에서는 네 배 넘게 늘려 쓰는 그림이라
    /// 딱 끊으면 원 둘레에 계단이 진다.
    /// </summary>
    private static Sprite IrisSprite()
    {
        const int N = 512;
        const float Radius = N * 0.25f;
        const float Edge = 1.5f;

        var made = new Color[N * N];
        float c = (N - 1) * 0.5f;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                made[y * N + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((d - Radius) / Edge));
            }
        }

        // 픽셀아트가 아니라 크게 늘려 쓰는 그림이라 보간을 켠 채로 읽는다.
        string path = SaveGenerated("Iris", EncodePng(made, N, N));
        return path == null ? null : LoadPhotoSprite(path);
    }

    /// <summary>
    /// 「꾹 눌러서 넘기기」 도넛 그림. 안쪽이 뚫린 흰 고리 한 장이다.
    ///
    /// 크게 늘려 쓰는 그림이 아니라 64칸 그대로 쓴다. 가장자리를 한 칸씩 부드럽게 해서
    /// 원이 톱니처럼 보이지 않게 한다 — 픽셀아트지만 원은 계단이 유독 눈에 띈다.
    /// </summary>
    private static Sprite RingSprite()
    {
        const int N = 64;
        const float Outer = 30f;
        const float Inner = 22f;
        const float Edge = 1.2f;

        var made = new Color[N * N];
        float c = (N - 1) * 0.5f;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                // 바깥에서 들어오고 안쪽에서 빠지는 두 경사를 곱해 고리를 만든다.
                float a = Mathf.Clamp01((Outer - d) / Edge) * Mathf.Clamp01((d - Inner) / Edge);
                made[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        }

        string path = SaveGenerated("SkipRing", EncodePng(made, N, N));
        return path == null ? null : LoadPhotoSprite(path);
    }

    /// <summary>
    /// 게이지가 앉는 자리(오른쪽 아래 구석, BottomRight 기준)와 크기.
    ///
    /// x 는 도넛이 아니라 **글자**가 정한다. 「꾹 눌러서 넘기기」 상자가 120칸인데 도넛 밑에
    /// 가운데로 붙으므로, 도넛을 -44 에 두면 글상자 오른끝이 화면 밖으로 16칸 넘친다.
    /// -64 면 글상자가 -124 ~ -4 로 안에 들어온다.
    /// </summary>
    private static readonly Vector2 SkipGaugeSize = new Vector2(48f, 48f);
    private static readonly Vector2 SkipGaugePos = new Vector2(-64f, 56f);

    /// <summary>글자가 도넛 아래로 내려앉는 거리.</summary>
    private const float SkipLabelDrop = 36f;

    /// <summary>주문 화면(180)보다 앞이라야 연출 위에 뜬다. 팝업(185)보다는 뒤여도 된다.</summary>
    private const int SkipGaugeOrder = 184;

    /// <summary>
    /// 「꾹 눌러서 넘기기」 게이지. 연출 중에만 스스로 떠 있고 평소에는 꺼져 있다.
    ///
    /// 바탕 고리를 흐리게 깔고 그 위에 차오르는 고리를 얹는다. 바탕이 없으면 반쯤 찬
    /// 게이지가 "반쪽짜리 호" 로만 보여서 얼마나 남았는지가 안 읽힌다.
    /// </summary>
    private static void BuildHoldToSkip(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();
        Sprite ringSprite = RingSprite();

        Transform root = CreateGroup("HoldToSkip", canvas);
        var own = Undo.AddComponent<Canvas>(root.gameObject);
        own.overrideSorting = true;
        own.sortingOrder = SkipGaugeOrder;

        // 껐다 켜는 것은 이 안쪽이다. 스크립트가 붙은 root 를 끄면 Poll 이 멈춘다.
        Transform panel = CreateGroup("Panel", root);

        Image track = CreateImage("Track", panel, BottomRight, SkipGaugePos, SkipGaugeSize,
                                  new Color(1f, 1f, 1f, 0.25f), ringSprite);
        track.raycastTarget = false;

        Image ring = CreateImage("Ring", panel, BottomRight, SkipGaugePos, SkipGaugeSize,
                                 Color.white, ringSprite);
        ring.raycastTarget = false;
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = true;
        ring.fillAmount = 0f;

        Vector2 labelPos = new Vector2(SkipGaugePos.x, SkipGaugePos.y - SkipLabelDrop);
        var label = CreateTmpText("Label", panel, BottomRight, labelPos,
                                  new Vector2(120f, 18f), "꾹 눌러서 넘기기", TextBody, tmpFont);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.color = Color.white;
        AttachPixelOutline(label, panel, labelPos, tmpFont, BottomRight);

        var hold = Undo.AddComponent<HoldToSkip>(root.gameObject);
        SetPrivateReference(hold, "root", panel.gameObject);
        SetPrivateReference(hold, "ring", ring);

        panel.gameObject.SetActive(false);
    }

    // 키 안내 아이콘 셋. 상단바 왼쪽 구석, 예전에 ? 버튼이 있던 자리다.
    //
    // 좌표는 TopLeft 기준. 글자가 아이콘 **아래**로 내려가서, 한 짝이 쓰는 폭은 아이콘 32 뿐이다.
    //   ESC  12~44
    //   B    52~84
    //   Tab  92~124
    // 그 오른쪽이 DayPanel 이다 — 판은 155 부터지만 시계 아이콘이 왼쪽 외곽선에 걸터앉아
    // 135 까지 나온다(AttachPanelIcon). Tab 오른끝 124 와 11칸이 뜬다.
    // 간격 40 을 더 벌리려면 DayPanel 을 같이 밀어야 하는데 그쪽은 25칸밖에 여유가 없다
    // (「마무리」가 380 부터). 그래서 벌리는 대신 좁혔다.
    private static readonly Vector2 KeyHintIconSize = new Vector2(32f, 32f);
    private static readonly Vector2 KeyHintEscPos = new Vector2(28f, -22f);
    private static readonly Vector2 KeyHintBookPos = new Vector2(68f, -22f);
    private static readonly Vector2 KeyHintTabPos = new Vector2(108f, -22f);

    /// <summary>
    /// 키 안내가 서는 층.
    ///
    /// 레시피북(OverlayPanelOrder 150)보다 **앞**이어야 한다. 글자를 아이콘 아래에 두면
    /// 화면 위 35칸부터를 덮는 레시피북 코일에 반쯤 잘린다 — 예전에 실제로 그래서 글자를
    /// 오른쪽으로 옮겼었다. 층을 올리면 자리를 바꾸지 않고도 위에 그려진다.
    /// 주문 화면(180)보다는 뒤라, 거기서는 KeyHintVisibility 가 없어도 가려진다.
    /// </summary>
    private const int KeyHintOrder = 151;

    /// <summary>
    /// 글자가 앉는 자리. 아이콘 한가운데에서 **아래로** 이만큼.
    ///
    /// 아이콘 아래끝이 16 이고 글자 상자가 18 이라, 26 이면 둘 사이가 1칸 뜬다.
    /// 예전에는 레시피북 코일에 잘려서 오른쪽에 붙였는데, 이제 키 안내가 레시피북보다
    /// 앞 층(KeyHintOrder)이라 아래에 둬도 가려지지 않는다.
    /// </summary>
    private const float KeyHintLabelY = -26f;

    /// <summary>
    /// Tab·B 키가 있다는 것을 알리는 아이콘 둘.
    ///
    /// 화면 어디에도 이 두 키에 대한 단서가 없어서, 튜토리얼에서 배운 뒤 잊으면 다시 알 길이 없다.
    /// 늘 보이게 둔다.
    /// </summary>
    private static void BuildKeyHints(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        // 왼쪽부터 ESC · B · Tab.
        //
        // ESC 만 주문 화면에서도 남는다(hideOnOrderScreen: false). 설정은 조리 중이 아니어도
        // 열려야 하는데 — 손님 대사를 읽다가 소리를 줄이고 싶은 자리가 그렇다 — Tab·B 와 같이
        // 감춰 두면 그때 열 길이 키보드밖에 없다. 층은 LiftEdgeUi 가 183 으로 올려 주므로
        // 주문 화면(180) 위에 그대로 뜬다. 주문 화면의 DayTimePanel 은 x 255 부터라 안 겹친다.
        Image esc = BuildKeyHint(canvas, "KeyHint_Esc", "Icon_Gear.png", "ESC", TopLeft, KeyHintEscPos,
                                 tmpFont, hideOnOrderScreen: false);
        Image book = BuildKeyHint(canvas, "KeyHint_Book", "Icon_Book.png", "B", TopLeft, KeyHintBookPos, tmpFont);
        Image tab = BuildKeyHint(canvas, "KeyHint_Tab", "Icon_Bill.png", "Tab", TopLeft, KeyHintTabPos, tmpFont);

        // 아이콘을 눌러도 열린다. 키를 모르는 사람은 아이콘이 떠 있어도 누를 생각을 못 한다.
        // CookingHotkeys 를 거쳐야 「조리 중일 때만」 판정이 한 곳에만 남는다.
        var hotkeys = Object.FindFirstObjectByType<CookingHotkeys>();
        if (hotkeys != null)
        {
            WireKeyHintButton(tab, hotkeys.ToggleOrderNote);
            WireKeyHintButton(book, hotkeys.ToggleRecipeBook);
        }
        else
        {
            Debug.LogWarning("[RamenLayoutBuilder] CookingHotkeys 를 찾지 못해 키 안내 아이콘이 클릭되지 않습니다.");
        }

        // ESC 는 여기서 안 잇는다 — 설정창이 아직 안 만들어졌다. WireEscHint 가 나중에 한다.

        // 세 아이콘을 레시피북(150)보다 앞 층에 올린다. 글자가 아이콘 아래에 있어서
        // 그냥 두면 왼쪽에서 나오는 레시피북 코일에 반쯤 잘린다.
        foreach (Image hint in new[] { tab, book, esc })
        {
            if (hint == null) continue;

            var lift = hint.gameObject.GetComponent<Canvas>();
            if (lift == null) lift = Undo.AddComponent<Canvas>(hint.gameObject);
            lift.overrideSorting = true;
            lift.sortingOrder = KeyHintOrder;

            // Canvas 를 얹으면 그 밑은 부모 레이캐스터가 못 닿는다. 아이콘을 누를 수 있어야 하므로 같이 단다.
            if (hint.gameObject.GetComponent<GraphicRaycaster>() == null)
                Undo.AddComponent<GraphicRaycaster>(hint.gameObject);
        }
    }

    /// <summary>
    /// ESC 아이콘에 설정창을 잇는다. 설정은 조리 중이 아니어도 열려야 해서
    /// Tab·B 와 달리 CookingHotkeys 를 안 거치고 SettingsUI.Open 을 곧장 부른다.
    ///
    /// BuildKeyHints 와 나눠 둔 이유는 순서다. 아이콘은 조리 화면과 함께 일찍 서고,
    /// 설정창은 시작 화면을 만들 때(BuildTitleScreen) 딸려 나온다.
    /// </summary>
    private static void WireEscHint(Transform canvas)
    {
        Transform found = FindDeep(canvas, "KeyHint_Esc");
        if (found == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] KeyHint_Esc 를 찾지 못했습니다.");
            return;
        }

        var settings = Object.FindFirstObjectByType<SettingsUI>(FindObjectsInactive.Include);
        if (settings == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] SettingsUI 를 찾지 못해 ESC 아이콘이 클릭되지 않습니다.");
            return;
        }

        WireKeyHintButton(found.GetComponent<Image>(), settings.Open);
    }

    /// <summary>
    /// 키 안내 아이콘을 누를 수 있게 만든다.
    ///
    /// StyleButton 을 쓰지 않는다. 그쪽은 실루엣에서 테두리를 떠 붙이는데, 이 아이콘은 이미
    /// 흰 테를 두른 스티커라 테가 두 겹이 된다. 색만 눌리게 두고 테는 안 건드린다.
    /// </summary>
    private static void WireKeyHintButton(Image icon, UnityEngine.Events.UnityAction action)
    {
        if (icon == null) return;

        // 만들 때 꺼 두었다. 눌리려면 다시 켜야 한다.
        icon.raycastTarget = true;

        var button = Undo.AddComponent<Button>(icon.gameObject);
        button.targetGraphic = icon;

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.875f, 0.875f, 0.875f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    /// <param name="hideOnOrderScreen">
    /// 주문 화면에서 감출지. Tab·B 는 거기서 키가 안 먹으니 감추고,
    /// ESC(설정)는 어느 화면에서나 열려야 하므로 그대로 둔다.
    /// </param>
    private static Image BuildKeyHint(Transform canvas, string name, string iconFile, string key,
                                      Vector2 anchor, Vector2 pos, TMP_FontAsset tmpFont,
                                      bool hideOnOrderScreen = true)
    {
        Image icon = CreateImage(name, canvas, anchor, pos, KeyHintIconSize,
                                 Color.white, LoadSprite(GeneratedDir + iconFile));
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // 주문 화면에서는 감춘다. 거기서는 두 키가 안 먹는다.
        if (hideOnOrderScreen)
        {
            Undo.AddComponent<CanvasGroup>(icon.gameObject);
            Undo.AddComponent<KeyHintVisibility>(icon.gameObject);
        }

        // 본문(12)이 쓸 수 있는 가장 작은 크기다. 사이 값은 획이 반칸에 걸려 흐려진다.
        Vector2 labelPos = new Vector2(0f, KeyHintLabelY);
        var label = CreateTmpText("Label", icon.transform, Center, labelPos,
                                  new Vector2(44f, 18f), key, TextBody, tmpFont);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.color = Color.white;

        AttachPixelOutline(label, icon.transform, labelPos, tmpFont);

        // 튜토리얼에서 "이 키를 눌러 보세요" 할 때 빛난다.
        //
        // ESC 는 튜토리얼이 가리키지 않으므로 아예 안 만든다. TutorialOutline.Kind 에는
        // TabKey·BookKey 둘뿐이라, 만들면 ESC 가 B 와 같은 때에 같이 빛난다.
        //
        // 다른 것들처럼 실루엣에서 테두리를 새로 뜨지 않는다. 이 아이콘은 이미 흰 테를 두른
        // 스티커라, 그 흰 테만 떠서 겹쳐 놓고 색을 돌리면 원래 테가 무지개로 물드는 것처럼 보인다.
        // 테두리를 하나 더 두르면 흰 테 바깥에 또 한 겹이 생겨 두툼해진다.
        if (key != "Tab" && key != "B") return icon;

        Sprite rim = WhiteRimSprite(icon.sprite, name);
        if (rim == null) return icon;

        Image glow = CreateImage("TutorialOutline", icon.transform, Center, Vector2.zero,
                                 KeyHintIconSize, Color.white, rim);
        glow.preserveAspect = true;
        glow.raycastTarget = false;
        glow.enabled = false;

        var mark = Undo.AddComponent<TutorialOutline>(glow.gameObject);
        mark.kind = key == "Tab" ? TutorialOutline.Kind.TabKey : TutorialOutline.Kind.BookKey;
        mark.lift = LiftCanvas(icon.gameObject);

        return icon;
    }

    // ── 완벽한 한 그릇 팻말 ───────────────────────────────────────

    /// <summary>결과창보다 앞, 시작 화면보다는 뒤.</summary>
    private const int PerfectSignOrder = 190;

    /// <summary>팻말 크기. 구운 그림(`완벽팻말.png`)과 **같아야** 한다 — 다르면 늘어나 흐려진다.</summary>
    private static readonly Vector2 PerfectSignSize = new Vector2(344f, 84f);

    /// <summary>흩어지는 반짝임 한 개의 크기.</summary>
    private static readonly Vector2 PerfectSparkleSize = new Vector2(13f, 13f);

    /// <summary>팻말이 서는 자리. 그릇보다 위, 손님 얼굴은 안 가리는 높이다.</summary>
    private const float PerfectSignY = 40f;

    /// <summary>
    /// 반짝임 한 장. 십자에 네 귀퉁이 점을 찍은 13x13 이다.
    ///
    /// 픽셀아트라 부드러운 빛 그림은 결이 안 맞는다. 획이 한 칸인 십자가 이 화면에 어울린다.
    /// </summary>
    private static Sprite SparkleSprite()
    {
        const int S = 13;
        int c = S / 2;

        var made = new Color[S * S];
        var clear = new Color(0f, 0f, 0f, 0f);
        var warm = new Color(1f, 0.94f, 0.63f, 1f);

        for (int i = 0; i < made.Length; i++) made[i] = clear;

        for (int i = 0; i < S; i++)
        {
            made[c * S + i] = warm;   // 가로획
            made[i * S + c] = warm;   // 세로획
        }

        // 네 귀퉁이에 점을 찍어 십자가 아니라 반짝임으로 읽히게 한다.
        made[(c - 1) * S + (c - 1)] = warm;
        made[(c - 1) * S + (c + 1)] = warm;
        made[(c + 1) * S + (c - 1)] = warm;
        made[(c + 1) * S + (c + 1)] = warm;

        string path = SaveGenerated("Sparkle", EncodePng(made, S, S));
        return path == null ? null : LoadSprite(path);
    }

    /// <summary>
    /// 정확도 100% 일 때 팍 떴다가 스윽 사라지는 팻말(기획서 v1.2 7.3).
    ///
    /// 시작 화면 버튼과 같은 나무판을 쓴다. 새 그림을 만들지 않고도 결이 맞는다.
    /// </summary>
    private static void BuildPerfectSign(Transform canvas)
    {
        // 글자가 그림에 들어 있어 여기서는 폰트도 아이콘 시트도 안 쓴다.
        Transform root = CreateGroup("PerfectSign", canvas);

        var own = Undo.AddComponent<Canvas>(root.gameObject);
        own.overrideSorting = true;
        own.sortingOrder = PerfectSignOrder;

        // 팻말은 글자까지 든 그림 한 장이다(Tools/make_perfect_sign.py, 344x84).
        //
        // 예전에는 **버튼용 나무 아이콘(Icon_Wood)을 300x56 으로 늘려 회색 톤을 입히고**
        // 그 위에 TMP 글자를 얹었다. 게다가 외곽선을 내려고 같은 글자를 여덟 벌 더 깔아
        // 팻말 하나에 TMP 가 아홉 개 붙어 있었다. 그림이면 한 장이다.
        //
        // 붉은 옻칠인 까닭 — 나무 현판으로 구웠더니 **조리 화면 배경이 나무판**이라
        // 벽에 뚫린 구멍처럼 묻혔다. 배경과 같은 재질은 피해야 한다.
        Image board = CreateImage("Board", root, Center, new Vector2(0f, PerfectSignY),
                                  PerfectSignSize, Color.white,
                                  LoadSprite(GeneratedDir + "완벽팻말.png"));
        board.raycastTarget = false;

        var group = Undo.AddComponent<CanvasGroup>(board.gameObject);
        group.blocksRaycasts = false;
        group.interactable = false;

        // 뜨는 순간 화면을 한 번 덮는 흰 판. 팻말보다 먼저 만들어 뒤에 둔다 —
        // 앞에 두면 번쩍이는 동안 팻말 글자가 하얗게 묻힌다.
        Image flash = CreateImage("Flash", root, Center, Vector2.zero, ScreenCover,
                                  new Color(1f, 1f, 1f, 0f));
        flash.raycastTarget = false;
        flash.enabled = false;
        flash.transform.SetAsFirstSibling();

        // 바깥으로 흩어지는 반짝임 여덟. 자리는 PerfectSign 이 매 프레임 잡는다.
        var sparkles = new RectTransform[8];
        for (int i = 0; i < sparkles.Length; i++)
        {
            Image spark = CreateImage("Sparkle" + i, board.transform, Center, Vector2.zero,
                                      PerfectSparkleSize, Color.white, SparkleSprite());
            spark.raycastTarget = false;
            spark.gameObject.SetActive(false);
            sparkles[i] = spark.rectTransform;
        }

        var sign = Undo.AddComponent<PerfectSign>(root.gameObject);
        SetPrivateReference(sign, "sign", board.rectTransform);
        SetPrivateReference(sign, "group", group);
        SetPrivateReference(sign, "flash", flash);
        SetPrivateArray(sign, "sparkles", sparkles);

        board.gameObject.SetActive(false);
    }

    // ── 주문마감 글자 ─────────────────────────────────────────────

    /// <summary>
    /// 검은 판(<see cref="ScreenFadeOrder"/>)보다 위다.
    /// 화면이 검게 물든 뒤에도 글자는 그 위에 남았다가 스러져야 한다.
    /// </summary>
    private const int ClosedSignOrder = 330;

    /// <summary>글자 넉 장의 파일 앞자리. 뒤에 _0 … _3 이 붙는다.</summary>
    private const string ClosedGlyphPrefix = UiDir + "주문마감_";

    /// <summary>
    /// 「주문마감」 넉 장의 크기(칸). <c>Tools/make_closed_sign.py</c> 가 찍어 준다.
    ///
    /// 붓글씨 한 장을 글자마다 갈라 구운 것이라 원본에서 잰 크기 그대로 써야 한다.
    /// 그림을 다시 구우면 스크립트가 찍어 주는 값으로 이 표도 같이 고칠 것.
    /// </summary>
    private static readonly Vector2[] ClosedGlyphSizes =
    {
        new Vector2(176f, 200f),
        new Vector2(164f, 194f),
        new Vector2(182f, 188f),
        new Vector2(164f, 198f),
    };

    /// <summary>
    /// 네 글자가 설 자리. 말 한가운데를 원점으로 잰 값이라 화면 크기가 바뀌어도 안 틀어진다.
    /// 글자마다 폭도 붓끝이 뻗은 정도도 달라서 고르게 벌릴 수가 없다 — 원본에서 잰 자리다.
    /// </summary>
    private static readonly Vector2[] ClosedGlyphSpots =
    {
        new Vector2(-253f, -8f),
        new Vector2(-83f, -6f),
        new Vector2(94f, -7f),
        new Vector2(268f, 0f),
    };

    /// <summary>
    /// 오늘 장사가 끝났다는 글자. 마지막 손님이 나간 뒤 한 자씩 퉁 하고 박힌다.
    ///
    /// 로고와 같은 손으로 그린 붓글씨다. 판도 테두리도 깔지 않는다 — 획에 짙은 테가 이미
    /// 그려져 있어 노렌 위에서도 야경 위에서도 뜬다.
    /// 넉 장으로 갈라 둔 까닭은 한 자씩 박기 위해서다. 박는 박자는 <see cref="ClosedSign"/> 이 쥔다.
    ///
    /// 자리는 화면 한가운데다. 이 글자가 뜰 때는 손님이 이미 스러져 나간 뒤다.
    /// </summary>
    private static void BuildClosedSign(Transform canvas)
    {
        Transform root = CreateGroup("ClosedSign", canvas);

        var own = Undo.AddComponent<Canvas>(root.gameObject);
        own.overrideSorting = true;
        own.sortingOrder = ClosedSignOrder;

        // 넉 장을 한꺼번에 흐리게 할 때 쓴다.
        var group = Undo.AddComponent<CanvasGroup>(root.gameObject);
        group.blocksRaycasts = false;
        group.interactable = false;

        var glyphs = new RectTransform[ClosedGlyphSizes.Length];
        for (int i = 0; i < glyphs.Length; i++)
        {
            Image glyph = CreateImage("Glyph" + i, root, Center, ClosedGlyphSpots[i], ClosedGlyphSizes[i],
                                      Color.white, LoadSprite(ClosedGlyphPrefix + i + ".png"));
            glyph.raycastTarget = false;
            glyph.gameObject.SetActive(false);   // ClosedSign 이 한 자씩 켠다
            glyphs[i] = glyph.rectTransform;
        }

        // 판은 켜 둔다. 꺼 두면 그 위에 붙은 ClosedSign 이 코루틴을 못 돌린다.
        var sign = Undo.AddComponent<ClosedSign>(root.gameObject);
        SetPrivateReference(sign, "group", group);
        SetPrivateArray(sign, "glyphs", glyphs);
    }

    // ── 시작 화면 ─────────────────────────────────────────────────
    //
    // 로고는 가운데 위, 버튼 넷은 아래에 한 줄로 왼쪽부터 나란히.
    // 나무판 버튼은 버튼_타이틀 / 버튼_타이틀_눌림 두 장을 유니티 Button 의 SpriteSwap 에 물린다.
    // 눌린 그림이 따로 있는 이유가 그것이라, 코드로 색을 어둡게 하는 것보다 결이 맞는다.

    /// <summary>
    /// 로고 크기. 원본 1983x793 의 비율(2.5:1)을 지켜 줄인 값이다.
    ///
    /// 620 에서 480 으로 줄였다. 배경이 영상으로 바뀌면서 로고가 포장마차 지붕과 노렌을
    /// 덮어 버렸다 — 배경을 바꾼 의미가 없어진다. 480 이면 로고가 하늘·건물 쪽에 앉는다.
    /// </summary>
    /// <remarks>
    /// 작다는 말이 나와 480 → 739 로 키웠다. 위아래가 둘 다 김에 걸려 있다.
    ///
    ///   위 — 로고 오른쪽 위 그릇에서 김이 올라간다. 그림 맨 윗줄(원본 y=37)이 그 김 끝이라,
    ///        판을 키우고 올리면 화면 밖으로 잘린다. 지금은 캔버스 254 로 위변에서 16칸 남는다.
    ///   아래 — 천막(붉은 노렌)은 덮어도 된다. 그 아래 카운터 냄비에서 오르는 김이 한계다.
    ///        노렌 아랫변을 배경에서 재니 캔버스 12~15 였고, 로고 아래끝을 15 에 맞췄다.
    ///
    /// 두 선 사이가 300 높이다. 크기나 자리를 손대면 둘 다 다시 재야 한다.
    /// </remarks>
    private static readonly Vector2 TitleLogoSize = new Vector2(739f, 300f);

    /// <summary>
    /// 로고 뒤에 깔리는 불빛 크기. 로고(480x192)보다 넉넉히 커야 빛이 밖으로 번진다.
    /// 로고에 딱 맞추면 빛이 아니라 뒤에 깐 색판으로 보인다.
    /// </summary>
    private static readonly Vector2 TitleGlowSize = new Vector2(985f, 500f);

    /// <summary>로고 높이. 크기를 줄인 만큼 같이 올려야 가게 지붕 위에 앉는다.</summary>
    private const float TitleLogoY = 118f;
    private const float TitleButtonY = -190f;
    private static readonly Vector2 TitleButtonSize = new Vector2(200f, 44f);
    private const float TitleButtonGap = 20f;

    /// <summary>
    /// 시작 화면 버튼 판. 짙은 나무에 금테와 쇠못이다(Tools/make_title_buttons.py).
    ///
    /// 예전에는 <c>Icon.png</c> 의 46x16 조각을 200x44 로 늘려 썼다. 가로 4.3배·세로 2.75배라
    /// **나뭇결이 뭉개져 색면**이 됐고, 색까지 밝아 밤 배경 위에 혼자 떴다. 바로 위 로고는
    /// 짙은 나무 간판에 금색 붓글씨인데 버튼만 딴 게임 UI 였다.
    ///
    /// 64x44 타일을 테두리 (14, 12) 로 늘린다. 세로가 쓰는 크기와 같아 배율이 1이고,
    /// 쇠못은 좌우 테두리 영역 안에 있어서 가로로 늘려도 양 끝에 그대로 남는다.
    /// </summary>
    private static Sprite TitleButtonSprite()
    {
        return LoadSlicedSprite(GeneratedDir + "버튼_타이틀.png", TitleButtonBorder);
    }

    /// <summary>눌린 판. 빛과 그늘이 위아래로 뒤집혀 실제로 눌려 들어간 모양이 된다.</summary>
    private static Sprite TitleButtonPressedSprite()
    {
        return LoadSlicedSprite(GeneratedDir + "버튼_타이틀_눌림.png", TitleButtonBorder);
    }

    private static readonly Vector4 TitleButtonBorder = new Vector4(14f, 12f, 14f, 12f);

    /// <summary>
    /// 버튼 글자색. 순백에서 금빛 도는 크림으로 내렸다.
    /// 금테와 같은 계열이라야 판이 한 덩어리로 읽힌다.
    /// </summary>
    private static readonly Color TitleButtonInkColor = new Color32(244, 220, 170, 255);

    private static void BuildTitleScreen(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("TitleScreen", canvas);

        // 자기 Canvas 로 맨 앞에 그린다. 튜토리얼 어두운 판보다도 앞이어야 한다.
        var own = Undo.AddComponent<Canvas>(root.gameObject);
        own.overrideSorting = true;
        own.sortingOrder = TitleOrder;
        Undo.AddComponent<GraphicRaycaster>(root.gameObject);

        // 배경. 비 오는 밤 포장마차가 도는 6초짜리 영상이다(24fps · 1280x720 · 소리 없음).
        //
        // 검은 판을 맨 뒤에 깐다. 영상은 판 크기(960x540)에 딱 맞춰 놓으므로, 창이 16:9 가
        // 아니면 그 바깥에 여백이 생긴다. 배경 그림처럼 ScreenCover(1920x1080)로 늘려 덮으면
        // 영상이 가운데만 남고 네 귀퉁이가 잘려 나간다 — 구도를 잡아 만든 그림이라 그러면 안 된다.
        Image back = CreateImage("Backdrop", root, Center, Vector2.zero, ScreenCover, Color.black);
        back.raycastTarget = true;   // 뒤쪽 조리 화면이 눌리지 않게 막는다

        // 영상이 나오기 전 한 컷. 영상 첫 프레임을 그대로 구워 둔 것이라 넘어가는 순간이 안 보인다.
        // 영상이 안 열리는 자리에서는 이게 그대로 남아 배경 노릇을 한다.
        Image still = CreateImage("BackgroundStill", root, Center, Vector2.zero, DesignResolution,
                                  Color.white, LoadPhotoSprite(ScreenDir + "시작화면 첫프레임.png"));
        still.raycastTarget = false;

        BuildTitleVideo(root, still);

        // 간판 불빛. 로고보다 **먼저** 만들어 뒤에 깔린다.
        // 유니티 UI 색은 곱하는 값이라 로고를 원본보다 밝게 못 만든다. 밝힐 수 없으면
        // 옆에 빛을 놓는 수밖에 없다 — 이 한 장이 "불이 켜졌다" 를 만든다.
        Image logoGlow = CreateImage("LogoGlow", root, Center, new Vector2(0f, TitleLogoY),
                                     TitleGlowSize, Color.white,
                                     LoadPhotoSprite(ScreenDir + "로고 불빛.png"));
        logoGlow.raycastTarget = false;

        // 로고. 픽셀아트가 아니라 그린 그림이라 줄일 때 보간이 있어야 깨끗하다.
        Image logo = CreateImage("Logo", root, Center, new Vector2(0f, TitleLogoY), TitleLogoSize,
                                 Color.white, LoadPhotoSprite(UiDir + "게임로고.png"));
        logo.preserveAspect = true;
        logo.raycastTarget = false;

        // 꺼진 간판으로 있다가 뜸을 길게 두고 깜빡이다 켜지고, 그 뒤로는 등불처럼 숨 쉰다.
        // 배경이 도는 영상이라 로고만 멈춰 있으면 붙여 놓은 스티커로 보인다.
        var logoIntro = Undo.AddComponent<TitleLogoIntro>(logo.gameObject);
        SetPrivateReference(logoIntro, "glow", logoGlow);

        // 버튼은 줄 단위로 만든다. 처음 줄과 모드를 고르는 줄이 같은 자리에 겹쳐 있다가
        // [게임시작] 을 누르면 갈아 끼워진다. 화면을 새로 띄우지 않아 흐름이 끊기지 않는다.
        Transform mainRow = CreateGroup("MainRow", root);
        Button[] mainButtons = MakeButtonRow(mainRow, new[] { "게임시작", "설정", "크레딧", "나가기" },
                                             tmpFont);

        // 처음에는 버튼이 없다. 로고 연출이 끝나면 왼쪽부터 하나씩 솟아 나온다.
        var cascade = Undo.AddComponent<TitleButtonCascade>(mainRow.gameObject);
        SetPrivateReference(cascade, "waitFor", logoIntro);

        Transform modeRow = CreateGroup("ModeRow", root);
        Button[] modeButtons = MakeButtonRow(modeRow, new[] { "스토리 모드", "무한 모드", "뒤로" },
                                             tmpFont);

        // 이 줄도 같은 식으로 나온다. 버튼을 눌러 줄이 갈릴 때마다 토도도독이 난다.
        // 로고는 그때 이미 끝나 있으므로 기다리지도, 쉬지도 않고 바로 시작한다.
        SetPrivateReference(Undo.AddComponent<TitleButtonCascade>(modeRow.gameObject),
                            "waitFor", logoIntro);

        modeRow.gameObject.SetActive(false);

        // 설정·크레딧을 눌렀을 때 뜨는 쪽지. 아직 내용이 없다.
        Transform notice = CreateGroup("Notice", root);

        var noticeBack = CreateImage("Backdrop", notice, Center, Vector2.zero, ScreenCover,
                                     new Color(0f, 0f, 0f, 0.6f));
        noticeBack.raycastTarget = true;

        Image noticePanel = CreateImage("Panel", notice, Center, Vector2.zero, new Vector2(300f, 120f),
                                        Hex("#FFF8E7"), DialogBoxSprite());
        noticePanel.type = Image.Type.Sliced;
        noticePanel.pixelsPerUnitMultiplier = 1f;

        var noticeLabel = CreateTmpText("MessageText", noticePanel.transform, Center, new Vector2(0f, 18f),
                                        new Vector2(280f, 40f), "", TextBody, tmpFont);
        noticeLabel.color = PopupInkColor;

        Button noticeClose = MakeDialogButton("CloseButton", noticePanel.transform, new Vector2(0f, -26f),
                                              "닫기", Hex("#7BA7C7"), tmpFont);

        notice.gameObject.SetActive(false);

        // 설정창은 시작 화면 **바깥**에 세운다(캔버스 직속). 인게임에서도 같은 창을 쓰는데,
        // 여기 밑에 두면 시작 화면이 꺼지는 순간 설정창까지 같이 사라진다.
        SettingsUI settings = BuildSettingsPanel(canvas, tmpFont);

        var ui = Undo.AddComponent<TitleScreenUI>(root.gameObject);
        SetPrivateReference(ui, "root", root.gameObject);
        SetPrivateReference(ui, "settings", settings);
        SetPrivateReference(ui, "mainRow", mainRow.gameObject);
        SetPrivateReference(ui, "modeRow", modeRow.gameObject);
        SetPrivateReference(ui, "startButton", mainButtons[0]);
        SetPrivateReference(ui, "settingsButton", mainButtons[1]);
        SetPrivateReference(ui, "creditsButton", mainButtons[2]);
        SetPrivateReference(ui, "quitButton", mainButtons[3]);
        SetPrivateReference(ui, "storyButton", modeButtons[0]);
        SetPrivateReference(ui, "endlessButton", modeButtons[1]);
        SetPrivateReference(ui, "backButton", modeButtons[2]);
        SetPrivateReference(ui, "noticeRoot", notice.gameObject);
        SetPrivateReference(ui, "noticeText", noticeLabel);
        SetPrivateReference(ui, "noticeCloseButton", noticeClose);
    }

    /// <summary>시작 화면 배경 영상이 그려지는 판을 세운다.</summary>
    ///
    /// <remarks>
    /// <see cref="VideoPlayer"/> 는 화면에 직접 못 그린다. <see cref="RenderTexture"/> 한 장에
    /// 그려 놓고 <see cref="RawImage"/> 로 받아야 UI 계층 안에 들어온다.
    ///
    /// 소리는 끈다. 영상에 오디오 트랙이 붙어 있지만 평균 −46dB 로 사실상 무음이라,
    /// 켜 봐야 AudioSource 만 하나 더 붙는다.
    /// </remarks>
    private static RawImage BuildTitleVideo(Transform parent, Graphic still)
    {
        var go = new GameObject("BackgroundVideo", typeof(RectTransform), typeof(RawImage));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Center;
        rect.anchorMax = Center;
        rect.pivot = Center;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = DesignResolution;
        Undo.RegisterCreatedObjectUndo(go, "Build Title Video");

        var screen = go.GetComponent<RawImage>();
        screen.raycastTarget = false;
        screen.texture = EnsureTitleVideoTexture();

        // 꺼 둔 채로 저장한다. TitleVideo 가 첫 프레임이 나오면 켠다.
        // 켜 두면 Play 를 누르기 전 Game 뷰에 빈 판(검정)이 깔려 시작 화면이 고장 난 것처럼 보인다.
        screen.enabled = false;

        var player = Undo.AddComponent<VideoPlayer>(go);
        player.source = VideoSource.VideoClip;
        player.clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoDir + "게임_시작화면.mp4");
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = screen.texture as RenderTexture;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.isLooping = true;       // 첫 프레임과 끝 프레임이 거의 같아 이어 붙여도 안 튄다
        player.playOnAwake = true;
        player.waitForFirstFrame = true;

        if (player.clip == null)
            Debug.LogWarning("[RamenLayoutBuilder] 시작 화면 영상을 찾지 못했습니다: "
                             + VideoDir + "게임_시작화면.mp4");

        var swap = Undo.AddComponent<TitleVideo>(go);
        SetPrivateReference(swap, "screen", screen);
        SetPrivateReference(swap, "still", still);

        return screen;
    }

    /// <summary>
    /// 영상이 그려질 판. 없으면 만들어 둔다.
    ///
    /// 영상 원본과 같은 1280x720 로 잡는다. 더 키워 봐야 원본에 없는 것이 생기지는 않는다.
    /// </summary>
    private static RenderTexture EnsureTitleVideoTexture()
    {
        const string path = VideoDir + "시작화면.renderTexture";

        var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (existing != null) return existing;

        var created = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    // ── 설정 창 ───────────────────────────────────────────────────
    //
    // 배경음 · 효과음 · 화면 필터 세 줄이다. 셋 다 눈금 0~10 이라 같은 위젯 하나로 그린다
    // (Sfx.MaxVolumeStep 과 ScreenGrade.MaxStep 이 둘 다 10).
    //
    // **시작 화면과 인게임이 같은 창을 쓴다.** 그래서 시작 화면 밑이 아니라 캔버스 바로
    // 밑에 세운다 — 시작 화면 밑에 두면 인게임에서 그것이 꺼질 때 설정창까지 사라진다.
    //
    // 짙은 나무에 금테인 까닭 — 밤거리(시작 화면)와 밝은 나무 카운터(조리 화면) 두 곳에서
    // 다 읽혀야 한다. 종이는 카운터 위에서 묻히고 어두운 반투명은 밤거리에서 묻힌다.

    private static readonly Vector2 SettingsPanelSize = new Vector2(420f, 300f);
    private static readonly Vector2 SettingsPlaqueSize = new Vector2(180f, 44f);
    private static readonly Vector2 SettingsCloseSize = new Vector2(170f, 48f);
    private static readonly Vector2 StepButtonSize = new Vector2(28f, 28f);

    /// <summary>눈금 한 칸과 칸 사이. 열 칸이 들어가는 폭은 여기서 계산된다.</summary>
    private const float SettingsSegmentWidth = 12f;
    private const float SettingsSegmentGap = 3f;

    /// <summary>눈금이 앉는 우묵한 틀의 높이. 그림(눈금틀.png)과 같아야 안 늘어난다.</summary>
    private const float SettingsBarHeight = 22f;

    /// <summary>이름 칸 폭과 조각 사이 여백.</summary>
    private const float SettingsLabelWidth = 96f;
    private const float SettingsRowPad = 12f;

    /// <summary>줄 사이 간격과 첫 줄 높이(판 가운데 기준).</summary>
    private const float SettingsRowGap = 48f;
    private const float SettingsFirstRowY = 48f;

    private static SettingsUI BuildSettingsPanel(Transform parent, TMP_FontAsset tmpFont)
    {
        Transform root = CreateGroup("Settings", parent);

        // 자기 Canvas 로 시작 화면(200)보다 앞에 그린다. 인게임에서는 조리 화면 위에,
        // 시작 화면에서는 그 위에 떠야 하므로 계층 순서만으로는 안 된다.
        var own = Undo.AddComponent<Canvas>(root.gameObject);
        own.overrideSorting = true;
        own.sortingOrder = SettingsOrder;
        Undo.AddComponent<GraphicRaycaster>(root.gameObject);

        // 껐다 켜는 것은 이 안쪽이다. 스크립트가 붙은 root 를 끄면 Awake 가 아예 안 돌아
        // 버튼에 손이 안 붙는다(실제로 한 번 그렇게 만들어 눌러도 반응이 없었다).
        Transform sheet = CreateGroup("Sheet", root);

        var backdrop = CreateImage("Backdrop", sheet, Center, Vector2.zero, ScreenCover,
                                   new Color(0f, 0f, 0f, 0.6f));
        backdrop.raycastTarget = true;

        Image panel = CreateImage("Panel", sheet, Center, Vector2.zero, SettingsPanelSize,
                                  Color.white, LoadSlicedSprite(GeneratedDir + "설정판.png",
                                                                new Vector4(16f, 16f, 16f, 16f)));
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 1f;

        // 제목 명패. 글자는 굽지 않는다 — 판만 굽고 TMP 가 그 위에 쓴다.
        float plaqueY = SettingsPanelSize.y * 0.5f - 24f - SettingsPlaqueSize.y * 0.5f;
        Image plaque = CreateImage("TitlePlaque", panel.transform, Center, new Vector2(0f, plaqueY),
                                   SettingsPlaqueSize, Color.white,
                                   LoadSprite(GeneratedDir + "설정명패.png"));
        var title = CreateTmpText("TitleText", plaque.transform, Center, Vector2.zero,
                                  new Vector2(130f, 30f), "설정", TextTitle, tmpFont);
        title.color = TitleButtonInkColor;

        SettingsRow bgm = MakeSettingsRow("BgmRow", panel.transform, SettingsFirstRowY,
                                          "배경음", Sfx.MaxVolumeStep, tmpFont);
        SettingsRow sfx = MakeSettingsRow("SfxRow", panel.transform, SettingsFirstRowY - SettingsRowGap,
                                          "효과음", Sfx.MaxVolumeStep, tmpFont);
        SettingsRow filter = MakeSettingsRow("FilterRow", panel.transform,
                                             SettingsFirstRowY - SettingsRowGap * 2f,
                                             "화면 필터", ScreenGrade.MaxStep, tmpFont);

        // 닫기 버튼은 시작 화면 버튼과 같은 판이다. 같은 가족이라 따로 구울 것이 없다.
        float closeY = -SettingsPanelSize.y * 0.5f + 24f + SettingsCloseSize.y * 0.5f;
        Image closeImage = CreateImage("CloseButton", panel.transform, Center, new Vector2(0f, closeY),
                                       SettingsCloseSize, Color.white, TitleButtonSprite());
        closeImage.type = Image.Type.Sliced;
        closeImage.pixelsPerUnitMultiplier = 1f;

        Button close = Undo.AddComponent<Button>(closeImage.gameObject);
        close.targetGraphic = closeImage;
        StyleButton(close);
        close.transition = Selectable.Transition.SpriteSwap;

        SpriteState closeState = close.spriteState;
        closeState.pressedSprite = TitleButtonPressedSprite();
        closeState.selectedSprite = closeImage.sprite;
        closeState.highlightedSprite = closeImage.sprite;
        close.spriteState = closeState;

        var closeLabel = CreateTmpText("Label", closeImage.transform, Center, Vector2.zero,
                                       new Vector2(SettingsCloseSize.x - 24f, 30f), "닫기", TextTitle, tmpFont);
        closeLabel.color = TitleButtonInkColor;

        var ui = Undo.AddComponent<SettingsUI>(root.gameObject);
        SetPrivateReference(ui, "root", sheet.gameObject);
        SetPrivateReference(ui, "bgmRow", bgm);
        SetPrivateReference(ui, "sfxRow", sfx);
        SetPrivateReference(ui, "filterRow", filter);
        SetPrivateReference(ui, "closeButton", close);

        sheet.gameObject.SetActive(false);

        return ui;
    }

    /// <summary>
    /// 설정 한 줄 — 이름 · ◀ · 눈금 막대 · ▶.
    ///
    /// 막대 폭은 칸 수에서 계산한다. 화면 필터와 볼륨이 우연히 둘 다 10 칸이지만,
    /// 한쪽이 바뀌어도 이 함수는 따라간다.
    /// </summary>
    private static SettingsRow MakeSettingsRow(string name, Transform parent, float y,
                                               string label, int steps, TMP_FontAsset tmpFont)
    {
        Transform row = CreateGroup(name, parent);
        ((RectTransform)row).anchoredPosition = new Vector2(0f, y);

        float barWidth = steps * SettingsSegmentWidth + (steps - 1) * SettingsSegmentGap + 6f;

        // 왼쪽부터 이름 · ◀ · 막대 · ▶ 를 차례로 놓고, 전체를 판 가운데에 맞춘다.
        float labelWidth = SettingsLabelWidth;
        float pad = SettingsRowPad;
        float total = labelWidth + pad + StepButtonSize.x + pad + barWidth + pad + StepButtonSize.x;
        float x = -total * 0.5f;

        var text = CreateTmpText("Label", row, Center, new Vector2(x + labelWidth * 0.5f, 0f),
                                 new Vector2(labelWidth, 24f), label, TextHead, tmpFont);
        text.color = TitleButtonInkColor;
        text.alignment = TextAlignmentOptions.Left;
        x += labelWidth + pad;

        Button minus = MakeStepButton("MinusButton", row, new Vector2(x + StepButtonSize.x * 0.5f, 0f), true);
        x += StepButtonSize.x + pad;

        // 우묵한 틀. 눈금이 그 안에 앉아 있어야 「얼마나 찼는지」가 읽힌다.
        Image well = CreateImage("Bar", row, Center, new Vector2(x + barWidth * 0.5f, 0f),
                                 new Vector2(barWidth, SettingsBarHeight), Color.white,
                                 LoadSlicedSprite(GeneratedDir + "눈금틀.png", new Vector4(5f, 5f, 5f, 5f)));
        well.type = Image.Type.Sliced;
        well.pixelsPerUnitMultiplier = 1f;
        well.raycastTarget = false;

        // 눈금 칸에는 그림이 없다. 색만 바꾸면 되는 네모라 흰 판에 색을 입힌다.
        var segments = new Image[steps];
        float first = -barWidth * 0.5f + 3f + SettingsSegmentWidth * 0.5f;
        for (int i = 0; i < steps; i++)
        {
            float sx = first + i * (SettingsSegmentWidth + SettingsSegmentGap);
            segments[i] = CreateImage("Seg" + i, well.transform, Center, new Vector2(sx, 0f),
                                      new Vector2(SettingsSegmentWidth, SettingsBarHeight - 8f), Color.white);
            segments[i].raycastTarget = false;
        }

        // 0 일 때만 뜨는 글자. 빈 막대만 있으면 고장 난 것으로 읽힌다.
        var off = CreateTmpText("OffText", well.transform, Center, Vector2.zero,
                                new Vector2(barWidth - 6f, 16f), "꺼짐", TextBody, tmpFont);
        off.color = new Color32(176, 150, 110, 255);
        off.enabled = false;

        x += barWidth + pad;
        Button plus = MakeStepButton("PlusButton", row, new Vector2(x + StepButtonSize.x * 0.5f, 0f), false);

        var comp = Undo.AddComponent<SettingsRow>(row.gameObject);
        SetPrivateArray(comp, "segments", segments);
        SetPrivateReference(comp, "offText", off);
        SetPrivateReference(comp, "minusButton", minus);
        SetPrivateReference(comp, "plusButton", plus);

        return comp;
    }

    /// <summary>
    /// 한 칸씩 옮기는 화살표 버튼. 24x24 고정이라 통짜 그림을 그대로 쓴다.
    ///
    /// 예전에는 안내용 삼각형을 90도 돌려 썼는데, 이제 왼쪽·오른쪽을 따로 구워 둬서
    /// 돌릴 필요가 없다(Tools/make_settings_panel.py). 눌린 그림도 같이 있다.
    /// </summary>
    private static Button MakeStepButton(string name, Transform parent, Vector2 pos, bool left)
    {
        string face = left ? "버튼_왼쪽" : "버튼_오른쪽";

        Image image = CreateImage(name, parent, Center, pos, StepButtonSize,
                                  Color.white, LoadSprite(GeneratedDir + face + ".png"));

        var button = Undo.AddComponent<Button>(image.gameObject);
        button.targetGraphic = image;
        StyleButton(button);
        button.transition = Selectable.Transition.SpriteSwap;

        SpriteState state = button.spriteState;
        state.pressedSprite = LoadSprite(GeneratedDir + face + "_눌림.png");
        state.selectedSprite = image.sprite;
        state.highlightedSprite = image.sprite;
        button.spriteState = state;

        return button;
    }

    /// <summary>버튼 한 줄. 가운데에 모아 놓고 좌우로 고르게 펼친다.</summary>
    private static Button[] MakeButtonRow(Transform parent, string[] labels, TMP_FontAsset tmpFont)
    {
        var buttons = new Button[labels.Length];

        // 두 장을 한 번만 읽어 줄 전체가 나눠 쓴다. 버튼마다 읽으면 임포터를 일곱 번 두드린다.
        Sprite plate = TitleButtonSprite();
        Sprite pressed = TitleButtonPressedSprite();

        float span = TitleButtonSize.x * labels.Length + TitleButtonGap * (labels.Length - 1);
        float first = -span * 0.5f + TitleButtonSize.x * 0.5f;

        for (int i = 0; i < labels.Length; i++)
        {
            float x = first + i * (TitleButtonSize.x + TitleButtonGap);
            buttons[i] = MakeWoodButton(labels[i], parent, new Vector2(x, TitleButtonY),
                                        plate, pressed, tmpFont);
        }

        return buttons;
    }

    /// <summary>
    /// 나무판 버튼 하나. 눌리면 그림이 어두운 판으로 바뀐다.
    ///
    /// 색을 곱하는 기본 방식(ColorTint) 대신 SpriteSwap 을 쓴다. 눌린 그림이 따로 그려져 있어서,
    /// 색만 어둡게 하는 것보다 나뭇결과 그림자가 실제로 눌린 모양이 된다.
    /// </summary>
    private static Button MakeWoodButton(string label, Transform parent, Vector2 pos,
                                         Sprite plate, Sprite pressed, TMP_FontAsset tmpFont)
    {
        // 색은 그림이 쥔다. 예전에는 밝은 나무 조각에 0.78 을 곱해 어둡게 만들었는데,
        // 곱하기로 내린 색은 나뭇결까지 같이 눌러서 판이 납작해진다.
        //
        // 굵게(Bold)는 쓰지 않는다 — Galmuri 는 래스터 픽셀 폰트라 진짜 굵은 글꼴이 없고,
        // TMP 가 셰이더로 획을 부풀려 흉내 내면 획이 반픽셀에 걸려 가장자리에 회색이 낀다.
        Image image = CreateImage("Button_" + label, parent, Center, pos, TitleButtonSize,
                                  Color.white, plate);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;

        var button = Undo.AddComponent<Button>(image.gameObject);
        button.targetGraphic = image;

        // 내려가는 반응과 흰 테두리는 다른 버튼과 같게. 색 변화는 SpriteSwap 이 대신하므로
        // StyleButton 이 넣는 ColorBlock 은 쓰이지 않는다.
        StyleButton(button);
        button.transition = Selectable.Transition.SpriteSwap;

        SpriteState state = button.spriteState;
        state.pressedSprite = pressed;
        state.selectedSprite = plate;
        state.highlightedSprite = plate;
        button.spriteState = state;

        var text = CreateTmpText("Label", image.transform, Center, Vector2.zero,
                                 new Vector2(TitleButtonSize.x - 24f, 28f), label, TextTitle, tmpFont);
        text.color = TitleButtonInkColor;

        return button;
    }

    /// <summary>
    /// 튜토리얼이 도는 동안 화면 전체를 덮는 어두운 판.
    ///
    /// 클릭은 통과시킨다(raycastTarget 끔). 막으면 정작 눌러야 할 통까지 안 눌린다.
    /// 자기 Canvas 를 달아 계층과 무관하게 거의 맨 앞에 그린다. 위로 올라오는 것은
    /// 이보다 큰 sortingOrder 를 켜는 쪽뿐이다.
    /// </summary>
    private static void BuildTutorialDim(Transform canvas)
    {
        Transform root = CreateGroup("TutorialDim", canvas);

        var own = Undo.AddComponent<Canvas>(root.gameObject);
        own.overrideSorting = true;
        own.sortingOrder = TutorialDimOrder;

        Image cover = CreateImage("Cover", root, Center, Vector2.zero, ScreenCover,
                                  new Color(0f, 0f, 0f, 0.55f));
        cover.raycastTarget = false;
        cover.enabled = false;

        var dim = Undo.AddComponent<TutorialDim>(root.gameObject);
        SetPrivateReference(dim, "cover", cover);
    }

    /// <summary>
    /// Tab 주문서·B 레시피북처럼 잠깐 펼쳐지는 판을 그릇보다 앞으로 올린다.
    ///
    /// 그릇이 재료를 옮기는 동안 어두운 판 위로 올라와서(overrideSorting 켜짐),
    /// 그냥 두면 화면 계층상 뒤에 있는 그릇이 펼친 책을 덮는다.
    /// </summary>
    private static void LiftOverlay(GameObject target)
    {
        var canvas = target.GetComponent<Canvas>();
        if (canvas == null) canvas = Undo.AddComponent<Canvas>(target);

        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlayPanelOrder;

        if (target.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(target);
    }

    /// <summary>
    /// 주문 화면(180) 위로 팝업을 올린다.
    ///
    /// GraphicRaycaster 를 같이 단다. Canvas 만 달면 그 아래 버튼이 클릭을 못 받는다 —
    /// 그림은 가장 가까운 Canvas 에 등록되고 레이캐스터는 자기 Canvas 것만 훑기 때문이다.
    /// </summary>
    private static void LiftPopup(GameObject target)
    {
        var canvas = target.GetComponent<Canvas>();
        if (canvas == null) canvas = Undo.AddComponent<Canvas>(target);

        canvas.overrideSorting = true;
        canvas.sortingOrder = PopupOrder;

        if (target.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(target);
    }

    /// <summary>
    /// 어두운 판 위로 끌어올릴 수 있게 Canvas 를 하나 달아 둔다.
    ///
    /// 화면 계층에서 통들은 어두운 판보다 앞에 있어서 그냥 두면 판에 덮인다.
    /// overrideSorting 을 켜면 계층과 무관하게 sortingOrder 순서로 그려진다.
    /// 평소에는 꺼 두므로 그리기 순서가 지금과 달라지지 않는다.
    /// </summary>
    private static Canvas LiftCanvas(GameObject target)
    {
        var canvas = target.GetComponent<Canvas>();
        if (canvas == null) canvas = Undo.AddComponent<Canvas>(target);

        // overrideSorting 이 꺼져 있으면 sortingOrder 를 넣어도 남지 않는다(부모 값을 따라 0 으로 읽힌다).
        // 켜 두고 값을 넣은 다음 다시 끈다. 그래야 나중에 켤 때 이 값이 살아 있다.
        canvas.overrideSorting = true;
        canvas.sortingOrder = TutorialLiftOrder;
        canvas.overrideSorting = false;

        // Canvas 를 붙이면 그 밑의 그림이 이 Canvas 쪽으로 따로 등록된다. 그러면 위쪽
        // GraphicRaycaster 가 더 이상 찾지 못해 클릭이 아예 안 닿는다.
        // 실제로 이것 때문에 재료통이 하나도 안 집혔다. 자기 몫의 레이캐스터를 같이 달아 준다.
        if (target.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(target);

        return canvas;
    }

    /// <summary>
    /// 마무리 버튼에 튜토리얼 안내 테두리를 얹는다. 재료통과 달리 재료가 아니라
    /// "제출만 남은 상태" 에 반응한다.
    /// </summary>
    private static void BuildSubmitOutline(Image button, Sprite bar)
    {
        // 마무리 바는 48x16 그림을 200x36 으로 늘려 쓰는 9-슬라이스다. 테두리 그림에
        // 슬라이스를 안 주면 통째로 늘어나 선이 네 배 두꺼워지고 모서리가 뭉개진다.
        Vector4 border = bar != null ? bar.border : Vector4.zero;

        Sprite outline = OutlineFromSprite(bar, "Submit", TutorialOutlineThickness, border);
        if (outline == null) return;

        // 9-슬라이스 모서리는 원본 픽셀 그대로 찍히므로(1칸 = 1좌표) 사방 두께만큼만 키우면 된다.
        float pad = TutorialOutlineThickness * 2f;

        Image glow = CreateImage("TutorialOutline", button.transform, Center, Vector2.zero,
                                 button.rectTransform.sizeDelta + new Vector2(pad, pad),
                                 Color.white, outline);
        glow.raycastTarget = false;
        glow.enabled = false;

        var mark = Undo.AddComponent<TutorialOutline>(glow.gameObject);
        mark.kind = TutorialOutline.Kind.Submit;
        mark.lift = LiftCanvas(button.gameObject);
    }

    /// <summary>
    /// 재료를 집어 옮기는 동안 그릇을 어두운 판 위로 끌어올린다.
    ///
    /// 그릇에는 무지개 테두리를 두르지 않는다. 한 번 둘러 봤더니 그릇 테가 원래 붉은 띠라
    /// 그 위에 색이 도는 선이 겹쳐 겉돌았다. 밝아지는 것만으로 "여기에 넣어라"가 읽힌다.
    /// 그래서 그림 없이 표시만 붙인다 — TutorialOutline 은 Image 가 없으면 끌어올리기만 한다.
    /// </summary>
    private static void BuildBowlLift(Image bowl)
    {
        if (bowl == null) return;

        // 그림 없이 끌어올리기만 하는 표시로 둔 적이 있다. 그랬더니 재료를 집어도 그릇에
        // 아무 표시가 없어 어디에 넣으라는 것인지 안 보였다. 테두리를 되살린다.
        // 대신 켜지는 때가 짧아졌다 — 들고 있는 동안과 그릇이 받아들이는 동안뿐이다.
        Sprite outline = OutlineFromSprite(bowl.sprite, "Bowl", TutorialOutlineThickness);

        Transform marker = outline != null
            ? CreateImage("TutorialOutline", bowl.transform, Center, Vector2.zero,
                          OutlineBoxSize(outline, bowl.rectTransform.sizeDelta),
                          Color.white, outline).transform
            : CreateGroup("TutorialLift", bowl.transform);

        var glow = marker.GetComponent<Image>();
        if (glow != null)
        {
            glow.preserveAspect = true;
            glow.raycastTarget = false;
            glow.enabled = false;
        }

        var mark = Undo.AddComponent<TutorialOutline>(marker.gameObject);
        mark.kind = TutorialOutline.Kind.Bowl;
        mark.lift = LiftCanvas(bowl.gameObject);
    }

    /// <summary>
    /// 확인창 버튼에 튜토리얼 안내 테두리를 얹는다. 마무리 버튼과 같은 9-슬라이스 방식이다.
    ///
    /// 끌어올리지 않는다. 확인창 자체가 이미 팝업 순서(185)로 어두운 판 위에 있다.
    /// </summary>
    private static void BuildDialogOutline(Image button, TutorialOutline.Kind kind)
    {
        if (button == null || button.sprite == null) return;

        Sprite outline = OutlineFromSprite(button.sprite, "DialogButton",
                                           TutorialOutlineThickness, button.sprite.border);
        if (outline == null) return;

        float pad = TutorialOutlineThickness * 2f;

        Image glow = CreateImage("TutorialOutline", button.transform, Center, Vector2.zero,
                                 button.rectTransform.sizeDelta + new Vector2(pad, pad),
                                 Color.white, outline);
        glow.raycastTarget = false;
        glow.enabled = false;

        var mark = Undo.AddComponent<TutorialOutline>(glow.gameObject);
        mark.kind = kind;
    }

    /// <summary>
    /// 아틀라스에서 잘라 온 그림의 테두리를 뜬다. 아이콘들은 한 장에 여러 칸이 들어 있어
    /// 파일 전체가 아니라 그 칸(textureRect)만 봐야 한다.
    /// </summary>
    private static Sprite OutlineFromSprite(Sprite source, string name, int thickness,
                                            Vector4 border = default)
    {
        if (source == null) return null;

        string path = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(path)) return null;

        return OutlineSprite(path, name, source.textureRect, thickness, border);
    }

    /// <summary>
    /// 스티커 그림에 이미 둘러 있는 흰 테만 떠 온다. 크기·자리가 원본과 같아 그대로 겹쳐 놓으면
    /// 흰 테 자리에 정확히 포개진다. 색은 TutorialOutline 이 입힌다.
    ///
    /// 흰 테는 순백(255,255,255)이고 종이·속지는 가장 밝은 곳도 파랑이 227 이라 그 사이에서 갈린다.
    /// 색 압축 때문에 몇 단계씩 흔들릴 수 있어 넉넉히 잡았다.
    /// </summary>
    private static Sprite WhiteRimSprite(Sprite source, string name)
    {
        if (source == null) return null;

        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath)) return null;

        Texture2D texture = ReadableTexture(sourcePath);
        if (texture == null) return null;

        Rect crop = source.textureRect;
        int x0 = Mathf.RoundToInt(crop.x);
        int y0 = Mathf.RoundToInt(crop.y);
        int w = Mathf.RoundToInt(crop.width);
        int h = Mathf.RoundToInt(crop.height);

        if (w <= 0 || h <= 0) return null;

        Color[] src = texture.GetPixels(x0, y0, w, h);
        var made = new Color[w * h];
        var clear = new Color(0f, 0f, 0f, 0f);

        for (int i = 0; i < made.Length; i++)
        {
            Color c = src[i];
            bool white = c.a > 0.5f && c.r > 0.93f && c.g > 0.93f && c.b > 0.93f;
            made[i] = white ? Color.white : clear;
        }

        string path = SaveGenerated("Rim_" + name, EncodePng(made, w, h));
        return path == null ? null : LoadSprite(path);
    }

    /// <summary>
    /// 통 위에 튜토리얼 안내 테두리를 얹는다. 통과 크기·자리가 같아야 실루엣이 맞으므로
    /// 사방을 통에 붙여 늘린다. 처음에는 꺼져 있고 TutorialOutline 이 자기 차례에 켠다.
    /// </summary>
    private static void BuildTutorialOutline(Image bin, SlotDef def)
    {
        // 돌아가는 통(육수 냄비)은 한 장에 네 칸이 나란히 들어 있는 시트다. 통째로 뜨면
        // 네 칸의 윤곽이 한 그림에 겹쳐 찍혀 손잡이가 냄비 한가운데에 줄줄이 그려진다.
        // 화면에 나오는 것은 첫 칸뿐이므로 거기만 떠야 한다.
        Rect crop = def.CropRect;
        if (def.LoopCell > 0)
        {
            Texture2D sheet = ReadableTexture(def.BinPath);
            if (sheet == null) return;

            // 칸은 왼쪽 위부터 센다. 텍스처 좌표는 아래가 0 이라 첫 줄이 맨 위에 있다.
            crop = new Rect(0f, sheet.height - def.LoopCell, def.LoopCell, def.LoopCell);
        }

        Sprite outline = OutlineSprite(def.BinPath, def.Type + (def.IdSuffix ?? ""),
                                       crop, TutorialOutlineThickness);
        if (outline == null) return;

        Image glow = CreateImage("TutorialOutline", bin.transform, Center, Vector2.zero,
                                 OutlineBoxSize(outline, def.Size), Color.white, outline);
        glow.preserveAspect = bin.preserveAspect;
        glow.raycastTarget = false;
        glow.enabled = false;

        var mark = Undo.AddComponent<TutorialOutline>(glow.gameObject);
        mark.type = def.Type;
        mark.lift = LiftCanvas(bin.gameObject);
    }

    /// <summary>
    /// 아직 안 들어온 재료통에 얹는 자물쇠. 통이 어두워지는 것은 <see cref="SlotLock"/> 이 한다.
    ///
    /// 자물쇠는 통의 자식이라 통 색과 따로 논다(uGUI 는 색을 자식에게 물려주지 않는다).
    /// 통만 어두워지고 자물쇠는 밝게 남아 눈에 들어온다.
    ///
    /// 토스트는 여기서 못 꽂는다. 통보다 나중에 만들어져서(BuildIngredientToast) 아직 없다.
    /// 다 만든 뒤에 <see cref="WireSlotLocks"/> 가 한꺼번에 꽂는다.
    /// </summary>
    private static void BuildSlotLock(Image bin, SlotHover hover, SlotDef def)
    {
        Image padlock = CreateImage("Lock", bin.transform, Center, Vector2.zero,
                                    new Vector2(LockIconSize, LockIconSize), Color.white,
                                    LoadSprite(GeneratedDir + "자물쇠.png"));
        padlock.preserveAspect = true;
        padlock.raycastTarget = false;
        padlock.enabled = false;          // 잠긴 날에만 SlotLock 이 켠다

        var slotLock = Undo.AddComponent<SlotLock>(bin.gameObject);
        SetPrivateInt(slotLock, "type", (int)def.Type);
        SetPrivateReference(slotLock, "bin", bin);
        SetPrivateReference(slotLock, "padlock", padlock);
        SetPrivateReference(slotLock, "hover", hover);
    }

    /// <summary>
    /// 개발 키가 열려 있다는 표시. 화면 왼쪽 아래 구석에 작게 둔다.
    ///
    /// 조리대와 겹치지 않는 자리를 골랐다 — 왼쪽 아래는 육수 냄비 밑 여백이라 늘 비어 있다.
    /// 글자만 껐다 켜므로 오브젝트는 계속 살아 있어야 한다(꺼 두면 신호를 못 받는다).
    /// </summary>
    private static void BuildDevKeyBadge(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        var label = CreateTmpText("DevKeyBadge", canvas, BottomLeft, new Vector2(8f, 6f),
                                  new Vector2(260f, 16f), "개발 키 ON  ·  Ctrl+Shift+D 로 잠금",
                                  TextSmall, tmpFont);
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.rectTransform.pivot = BottomLeft;
        label.rectTransform.anchoredPosition = new Vector2(8f, 6f);
        label.color = new Color(1f, 0.85f, 0.3f, 0.85f);
        label.raycastTarget = false;
        label.enabled = false;

        LiftOverlay(label.gameObject);

        var badge = Undo.AddComponent<DevKeyBadge>(label.gameObject);
        SetPrivateReference(badge, "label", label);
    }

    /// <summary>
    /// 해금 연출이 쓰는 것들. 하루의 첫 조리 화면에서 자물쇠가 풀릴 때 뜬다.
    ///
    /// 어두운 판은 결이 Grand 일 때만 켜진다. 늘 만들어 두고 연출이 알아서 여닫는다 —
    /// 결을 바꿀 때마다 빌더를 다시 돌려야 하면 고르기가 번거롭다.
    /// </summary>
    private static void BuildUnlockPopup(Transform canvas, DayManager dayManager)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("UnlockPopup", canvas);
        LiftPopup(root.gameObject);

        // 연출이 도는 동안 조리대를 못 만지게 막는 투명 판. 안 보이지만 클릭은 다 삼킨다.
        // 연출이 끝나면 root 가 통째로 꺼지므로 따로 걷을 것이 없다.
        Image blocker = CreateImage("Blocker", root, Center, Vector2.zero, ScreenCover,
                                    new Color(0f, 0f, 0f, 0f));
        blocker.raycastTarget = true;

        // 판은 튜토리얼 안내판과 같은 그림이다. 새 모양을 만들면 「알려 주는 창」이 두 가지가 된다.
        // 색은 그림이 쥔다(반투명이라 알파까지 그림에 있다). 여기서 곱하면 두 번 어두워진다.
        Image panel = CreateImage("Panel", root, Center, Vector2.zero, new Vector2(420f, 64f),
                                  Color.white, TutorialPanelSprite());
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 1f;
        panel.raycastTarget = false;

        // 판 전체를 한꺼번에 흐리게 하려고 씌운다. 글자마다 색을 만지면 손댈 곳이 는다.
        var group = Undo.AddComponent<CanvasGroup>(panel.gameObject);
        group.blocksRaycasts = false;
        group.interactable = false;

        var title = CreateTmpText("TitleText", panel.transform, Center, new Vector2(0f, 18f),
                                  new Vector2(404f, 16f), "새 재료가 들어왔습니다", TextBody, tmpFont);
        title.color = DarkPanelInkColor;
        title.raycastTarget = false;

        var names = CreateTmpText("NamesText", panel.transform, Center, new Vector2(0f, -12f),
                                  new Vector2(404f, 28f), "김 · 계란", TextTitle, tmpFont);
        names.color = DarkPanelInkColor;
        names.raycastTarget = false;

        root.gameObject.SetActive(false);

        // 연출을 도는 쪽은 root 바깥에 있어야 한다. 안에 있으면 스스로를 꺼 버린다.
        DayManager manager = dayManager != null ? dayManager : Object.FindFirstObjectByType<DayManager>();
        if (manager == null) return;

        var sequence = manager.GetComponent<UnlockSequence>();
        if (sequence == null) sequence = Undo.AddComponent<UnlockSequence>(manager.gameObject);

        SetPrivateReference(sequence, "root", root.gameObject);
        SetPrivateReference(sequence, "panel", panel.rectTransform);
        SetPrivateReference(sequence, "title", title);
        SetPrivateReference(sequence, "names", names);
        SetPrivateReference(sequence, "openSprite", LoadSprite(GeneratedDir + "자물쇠_열림.png"));
        SetPrivateReference(sequence, "glowSprite", LoadSprite(GeneratedDir + "AuraGlow.png"));
        SetPrivateReference(sequence, "sparkleSprite", LoadSprite(GeneratedDir + "Sparkle.png"));

        // 연출을 돌리는 쪽은 자물쇠를 칠하는 쪽이다. 하루가 열릴 때 오늘 열 통을 골라 쥐고 있다가
        // 조리 화면이 뜨면 여기에 넘긴다.
        var locks = manager.GetComponent<IngredientLocks>();
        if (locks != null) SetPrivateReference(locks, "sequence", sequence);
    }

    /// <summary>
    /// 자물쇠들에 안내 토스트를 꽂고, 일차가 바뀔 때 다시 칠할 쪽을 세운다.
    /// 통·토스트·DayManager 가 모두 생긴 뒤에 부른다.
    /// </summary>
    private static void WireSlotLocks(Transform canvas, IngredientToast toast, DayManager dayManager)
    {
        foreach (SlotLock slotLock in canvas.GetComponentsInChildren<SlotLock>(true))
        {
            SetPrivateReference(slotLock, "toast", toast);
        }

        // 일차 신호를 받는 쪽. 재료통이 아니라 늘 살아 있는 오브젝트에 붙어야 한다 —
        // 통은 튜토리얼이나 팝업 때문에 꺼질 수 있고, 꺼져 있으면 신호를 못 받는다.
        DayManager manager = dayManager != null ? dayManager : Object.FindFirstObjectByType<DayManager>();
        if (manager == null) return;

        var locks = manager.GetComponent<IngredientLocks>();
        if (locks == null) locks = Undo.AddComponent<IngredientLocks>(manager.gameObject);
        SetPrivateReference(locks, "dayManager", manager);

        // 주문 화면이 닫히는 때가 곧 조리가 시작되는 때다. 해금 연출은 그때 돈다.
        SetPrivateReference(locks, "orderScreen", manager.GetComponent<OrderScreenUI>());
    }

    /// <summary>
    /// 통 그림의 실루엣 <b>바깥</b>에 테두리를 두른다. 튜토리얼에서 "지금 이걸 집으세요" 표시로 쓴다.
    ///
    /// 예전에는 안쪽으로 둘렀다. 그림 크기가 원본과 같아 상자를 그대로 써도 되기 때문인데,
    /// 테두리가 통 그림을 파먹어서 마무리 버튼처럼 테두리가 있는 그림에서는 제 테두리를
    /// 덮어 버렸다. 바깥으로 두르면 통은 온전히 남고 테두리가 통을 감싼다.
    ///
    /// 그 대가로 그림이 사방 thickness 만큼 커진다. 올릴 상자도 같은 비율로 키워야 자리가
    /// 맞는다 — <see cref="OutlineBoxSize"/> 가 그 계산이다.
    ///
    /// 9-슬라이스로 늘려 쓰는 그림(마무리 버튼·확인창 버튼)은 border 를 넘긴다. 그림이
    /// 사방 thickness 만큼 커졌으니 테두리 폭도 같이 키워야 모서리가 제자리에 찍힌다.
    ///
    /// 나오는 그림은 흰색 한 장이다. 색은 TutorialOutline 이 입힌다.
    /// </summary>
    private static Sprite OutlineSprite(string sourcePath, string name, Rect crop, int thickness,
                                        Vector4 border = default)
    {
        Texture2D texture = ReadableTexture(sourcePath);
        if (texture == null) return null;

        int x0 = crop.width > 0f ? Mathf.RoundToInt(crop.x) : 0;
        int y0 = crop.width > 0f ? Mathf.RoundToInt(crop.y) : 0;
        int sw = crop.width > 0f ? Mathf.RoundToInt(crop.width) : texture.width;
        int sh = crop.width > 0f ? Mathf.RoundToInt(crop.height) : texture.height;

        if (sw <= 0 || sh <= 0) return null;

        Color[] src = texture.GetPixels(x0, y0, sw, sh);

        // 테두리가 들어갈 자리를 사방에 둔다. 통이 그림 가장자리에 닿아 있으면 이 여백이
        // 없어 테두리가 잘린다. 재료통 그림은 실제로 여백을 잘라 낸 것이라 늘 닿아 있다.
        int w = sw + thickness * 2;
        int h = sh + thickness * 2;

        var made = new Color[w * h];
        var clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                made[y * w + x] = clear;

                int sx = x - thickness;
                int sy = y - thickness;

                // 그림이 있는 자리는 비운다. 테두리는 그림 바깥에만 그린다.
                bool inside = sx >= 0 && sy >= 0 && sx < sw && sy < sh && src[sy * sw + sx].a > 0.1f;
                if (inside) continue;

                // 두께 안에 그림이 하나라도 있으면 그 그림의 바깥 가장자리다.
                bool near = false;
                for (int dy = -thickness; dy <= thickness && !near; dy++)
                {
                    for (int dx = -thickness; dx <= thickness && !near; dx++)
                    {
                        int nx = sx + dx;
                        int ny = sy + dy;

                        if (nx < 0 || ny < 0 || nx >= sw || ny >= sh) continue;
                        if (src[ny * sw + nx].a > 0.1f) near = true;
                    }
                }

                if (near) made[y * w + x] = Color.white;
            }
        }

        string path = SaveGenerated("Outline_" + name, EncodePng(made, w, h));
        if (path == null) return null;

        return border == Vector4.zero
            ? LoadSprite(path)
            : LoadSlicedSprite(path, border + Vector4.one * thickness);
    }

    /// <summary>
    /// 통 그림을 <b>하얗게 채운 실루엣</b>. 마우스를 올렸을 때 통 위에 겹쳐 밝히는 데 쓴다.
    ///
    /// 통 그림 자체를 흰색으로 물들여 겹쳐 봤더니 아무 일도 안 일어났다 — 흰색 곱하기는
    /// 색을 안 바꾸므로 같은 그림이 한 번 더 그려질 뿐이다. 밝히려면 모양만 같고 속은
    /// 하얀 그림이 따로 있어야 한다.
    ///
    /// 알파는 원본 그대로 가져간다. 가장자리가 반투명한 그림도 그 부드러움이 남는다.
    /// </summary>
    private static Sprite SilhouetteSprite(string sourcePath, string name, Rect crop)
    {
        Texture2D texture = ReadableTexture(sourcePath);
        if (texture == null) return null;

        int x0 = crop.width > 0f ? Mathf.RoundToInt(crop.x) : 0;
        int y0 = crop.width > 0f ? Mathf.RoundToInt(crop.y) : 0;
        int w = crop.width > 0f ? Mathf.RoundToInt(crop.width) : texture.width;
        int h = crop.width > 0f ? Mathf.RoundToInt(crop.height) : texture.height;

        if (w <= 0 || h <= 0) return null;

        Color[] src = texture.GetPixels(x0, y0, w, h);
        var made = new Color[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            made[i] = new Color(1f, 1f, 1f, src[i].a);
        }

        string path = SaveGenerated("Fill_" + name, EncodePng(made, w, h));
        return path == null ? null : LoadSprite(path);
    }

    /// <summary>
    /// 바깥 테두리 그림을 올릴 상자 크기.
    ///
    /// 테두리 그림은 원본보다 사방 <see cref="TutorialOutlineThickness"/> 만큼 크다. 원본이
    /// 화면에서 늘어난 배율만큼 여백도 늘어나야 테두리가 통에 딱 붙는다.
    /// preserveAspect 로 그려지는 그림이라 가로세로 같은 배율을 쓴다.
    /// </summary>
    private static Vector2 OutlineBoxSize(Sprite outline, Vector2 artBox)
    {
        if (outline == null) return artBox;

        float pad = TutorialOutlineThickness * 2f;
        float w = outline.rect.width - pad;
        float h = outline.rect.height - pad;
        if (w <= 0f || h <= 0f) return artBox;

        float scale = Mathf.Min(artBox.x / w, artBox.y / h);
        return new Vector2(outline.rect.width, outline.rect.height) * scale;
    }

    /// <summary>
    /// 주문서 종이. 9-슬라이스로 쓸 6x6 이다.
    ///
    /// 테두리 두 칸씩을 원본 크기로 찍고 가운데만 늘리므로 종이가 아무리 커져도 선은 1픽셀이다.
    /// 아래 두 줄에는 가로 테두리를 긋지 않는다. 종이 아래변은 톱니 그림이 맡는다.
    /// </summary>
    private static Sprite NotePaperSprite()
    {
        string path = SaveGenerated("NotePaper", BuildNotePaperBytes());
        if (path == null) return null;

        return LoadSlicedSprite(path, new Vector4(2f, 2f, 2f, 2f));
    }

    private static byte[] BuildNotePaperBytes()
    {
        const int N = 6;
        var px = new Color[N * N];

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                // GetPixels·SetPixels 는 아래에서 위로 담긴다. y = N-1 이 맨 윗줄이다.
                bool side = x == 0 || x == N - 1;
                bool top = y == N - 1;

                px[y * N + x] = side || top ? NoteOutlineColor
                              : x == N - 2 ? NoteShadeColor
                              : NotePaperColor;
            }
        }

        return EncodePng(px, N, N);
    }

    /// <summary>
    /// 종이 아래 톱니. 가로로 깔아 쓰는 8x6 한 칸이다.
    ///
    /// 9-슬라이스로는 못 만든다. 늘리는 구간에 들어가면 톱니가 가로로 늘어나 뭉개진다.
    /// 그래서 Tiled 로 같은 칸을 반복해 깐다.
    /// </summary>
    private static Sprite NoteTornSprite()
    {
        string path = SaveGenerated("NoteTorn", BuildNoteTornBytes());
        if (path == null) return null;

        return LoadSprite(path);
    }

    private static byte[] BuildNoteTornBytes()
    {
        const int W = 8, H = 6;
        var px = new Color[W * H];          // 기본은 투명

        for (int x = 0; x < W; x++)
        {
            // 0 1 2 3 4 3 2 1 처럼 오르내린다. 한 칸이 끝나고 다음 칸이 시작해도 이어진다.
            int depth = Mathf.Abs((x + 3) % W - 4);

            for (int d = 0; d <= depth; d++) px[(H - 1 - d) * W + x] = NotePaperColor;

            int edge = H - 1 - (depth + 1);
            if (edge >= 0) px[edge * W + x] = NoteOutlineColor;
        }

        return EncodePng(px, W, H);
    }

    /// <summary>
    /// 구분선 점 한 칸. 4x1 에 두 칸만 찍혀 있고 이걸 가로로 깐다.
    /// 색은 흰색으로 두고 Image.color 로 입힌다.
    /// </summary>
    private static Sprite NoteRuleSprite()
    {
        string path = SaveGenerated("NoteRule", BuildNoteRuleBytes());
        if (path == null) return null;

        return LoadSprite(path);
    }

    private static byte[] BuildNoteRuleBytes()
    {
        var px = new Color[4];
        px[0] = Color.white;
        px[1] = Color.white;
        px[2] = new Color(0f, 0f, 0f, 0f);
        px[3] = new Color(0f, 0f, 0f, 0f);

        return EncodePng(px, 4, 1);
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

        #pragma warning disable 0618 // spritesheet 폐기 예고 — LoadIconSprites 의 설명 참조
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
        #pragma warning restore 0618

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
        LiftOverlay(root.gameObject);

        // 뒷배경을 어둡게 덮지 않는다. B를 누르고 있는 동안만 잠깐 올라오는 것이라
        // 화면을 가릴 이유가 없고, 조리하던 손이 끊긴다.

        // 판은 손으로 그린 수첩 한 장이다(Assets/Art/UI/레시피북.png, 900x1470).
        //
        // **글과 재료가 그림에 다 들어 있다.** 예전에는 빈 수첩을 굽고 그 위에 TMP 글자를
        // 얹었는데(Tools/make_recipe_book.py + RecipeBookUI.FillRecipeTable), 이제 제목·메뉴 이름·
        // 밑줄·재료가 전부 그려져 있어서 글자를 얹으면 겹쳐 찍힌다. 그래서 다 뺐다.
        //
        // 그 대가로 **레시피가 코드와 따로 논다.** 재료 구성을 코드에서 바꿔도 이 그림은 안 따라온다.
        // 바뀌면 그림을 다시 그려야 한다.
        //
        // 크기는 원본의 정확히 1/3 이다. 그림이 3배로 그려져 있어 1/3 로 줄여야 칸이 딱 맞는다.
        // 어중간한 배율로 줄이면 글자 획이 뭉갠다.
        Image panel = CreateImage("Panel", root, Center, new Vector2(0f, -530f), new Vector2(300f, 490f),
                                  Color.white, LoadPhotoSprite(UiDir + "레시피북.png"));

        // **책이 덮은 자리는 클릭을 막는다.** 예전에는 마우스를 올리면 판이 0.4 로 흐려져
        // 뒤의 타래 3통과 육수 냄비가 비쳐 보였고, 그래서 클릭도 통과시켰다. 지금은 늘 진해서
        // 뒤가 안 보이는데, 안 보이는 것을 모르고 집으면 엉뚱한 타래가 들어간다.
        // 판이 클릭을 삼키게 두면 책을 닫아야 조리를 잇게 된다.
        //
        // 책 밖(오른쪽 재료통)은 그대로 집힌다. 판이 덮은 자리만 막는 것이다.
        panel.raycastTarget = true;

        var bookFade = Undo.AddComponent<CanvasGroup>(panel.gameObject);
        bookFade.blocksRaycasts = true;
        bookFade.interactable = false;

        // 제목·메뉴 이름·밑줄·재료 글상자는 없다. 전부 그림에 그려져 있다.
        // RecipeBookUI 는 빈 배열을 받아 채울 것이 없으면 그냥 지나간다.
        var menuNameTexts = new TextMeshProUGUI[0];
        var menuValueTexts = new TextMeshProUGUI[0];

        // 마우스를 올린 재료의 이름. 글자 한 벌을 만들어 두고 RecipeBookUI 가 자리를 옮긴다.
        //
        // **묶음 안에 넣어 옮긴다.** AttachPixelOutline 은 검은 복제본을 형제로 만들고 자리를
        // 한 번만 잡아 준다. 글자만 옮기면 복제본은 제자리에 남아 외곽선이 따로 논다.
        // 묶음째 옮기면 아홉 장이 같이 간다.
        Transform nameGroup = CreateGroup("HoverName", panel.transform);
        var nameLabel = CreateTmpText("Label", nameGroup, Center, Vector2.zero,
                                      new Vector2(120f, 20f), "재료", TextBody, tmpFont);
        nameLabel.color = Color.white;
        nameLabel.raycastTarget = false;
        AttachPixelOutline(nameLabel, nameGroup, Vector2.zero, tmpFont);

        nameGroup.gameObject.SetActive(false);

        // 닫기 버튼은 두지 않는다. B 키를 떼면 저절로 접히므로 누를 일이 없고,
        // 종이 위에 얹힌 초록 판이 낙서 그림과 따로 놀았다.
        // RecipeBookUI.closeButton 은 비어 있어도 되게 막혀 있다.

        root.gameObject.SetActive(false);

        return new RecipeBookRefs
        {
            Root = root.gameObject,
            MenuNameTexts = menuNameTexts,
            MenuValueTexts = menuValueTexts,
            Close = null,
            Panel = panel.rectTransform,
            Fade = bookFade,
            HoverName = nameGroup as RectTransform,
            HoverLabel = nameLabel
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

        // 그릇·재료통이 자기 Canvas 로 앞에 나와 있어서, 주문 화면도 자기 Canvas 로
        // 그보다 앞에 서야 한 덩어리로 미끄러진다. 안 그러면 그릇만 판 위에 떠서 따라온다.
        var ownCanvas = Undo.AddComponent<Canvas>(root.gameObject);
        ownCanvas.overrideSorting = true;
        ownCanvas.sortingOrder = OrderScreenOrder;
        Undo.AddComponent<GraphicRaycaster>(root.gameObject);

        // 밤 배경. 불투명이라 조리 화면을 완전히 가리고, 레이캐스트도 여기서 막힌다.
        // 그림보다 크게 잡아 두어, 16:9가 아닌 창에서 판 바깥이 비어 보이지 않게 한다.
        var night = CreateImage("Night", root, Center, Vector2.zero, ScreenCover, DarkHex("#14100E"));
        night.raycastTarget = true;

        // 포장마차 그림. 노렌·뒷벽·카운터가 다 들어 있어서, 예전에 자리만 잡아 두었던
        // BackWall·CounterFront·CounterTop 색판 세 장을 이 한 장이 대신한다.
        // Night 와 달리 판(640x360)에 맞춘다. ScreenCover 로 두면 3배로 늘어나 가운데만 보인다.
        var scenery = CreateImage("Scenery", root, Center, Vector2.zero, DesignResolution,
                                  Color.white, LoadPhotoSprite(ScreenDir + "주문화면 배경.png"));
        scenery.raycastTarget = false;

        // 3컷에서만 켜지는 우주 배경. 포장마차를 통째로 덮되 손님보다는 뒤에 있어야 하므로
        // 여기서 만든다(유니티 UI 는 나중에 만든 형제가 위에 그려진다).
        // Scenery 와 같이 판(640x360)에 맞춘다. ScreenCover 로 두면 3배로 늘어나 가운데만 보인다.
        //
        // Scenery 와 달리 LoadPhotoSprite 를 쓰면 안 된다. 그쪽은 Bilinear 로 넣는데,
        // 1671x941 짜리를 640 으로 줄여 쓰는 그림에는 그게 맞지만 이 그림은 640x360 픽셀아트다.
        // 보간하면 1픽셀 별이 뭉개지고, 압축까지 걸리면 디더 무늬에 얼룩이 생긴다.
        //
        // 판이 아니라 네모(1104)로 잡는다. 연출이 이 판을 천천히 돌리는데, 판 크기 그대로 두면
        // 돌릴 때 구석이 비어 검은 삼각형이 드러난다. 한 변은 화면 대각선(약 1101)보다 커야 한다.
        // 그림도 같은 크기로 찍혀 있어 1:1로 얹힌다(Tools/makeart.py).
        Image cosmos = CreateImage("Cosmos", root, Center, Vector2.zero, CosmosSize,
                                   Color.white, LoadSprite(ScreenDir + "우주 배경.png"));
        cosmos.raycastTarget = false;
        cosmos.enabled = false;

        // 확대 직전에 가게를 어둡게 덮는 검은 판. 손님보다 뒤에 있어야 얼굴은 밝게 남는다.
        Image dim = CreateImage("Dim", root, Center, Vector2.zero, ScreenCover,
                                new Color(0f, 0f, 0f, 0f));
        dim.raycastTarget = false;
        dim.enabled = false;

        // 번개 한 방. 11시에서 5시로 가는 방향이 그림에 이미 구워져 있어 돌리지 않는다.
        // 손님보다 먼저 만들어 뒤에 깔리게 한다. 앞에 두면 번쩍일 때 얼굴을 가린다.
        Image bolt = CreateImage("Bolt", root, Center, Vector2.zero, DesignResolution,
                                 Color.white, LoadSprite(EtcDir + "큰번개.png"));
        bolt.raycastTarget = false;
        bolt.enabled = false;

        // 오우라 뒤에 까는 빛. 오우라보다 먼저 만들어 더 뒤에 깔린다.
        //
        // 고리만 있을 때는 얼굴이 밝은 채로 앞을 다 덮어서 "빛난다"가 안 읽혔다.
        // 화면 밖까지 번지는 빛을 한 겹 깔아 두면 고리가 그 위에 얹힌다.
        Image auraGlow = CreateImage("AuraGlow", root, Center, Vector2.zero, new Vector2(640f, 640f),
                                     Hex("#FFD98A"), RadialGlowSprite());
        auraGlow.raycastTarget = false;
        auraGlow.enabled = false;

        // 감동 오우라. 손님 뒤에서 퍼져야 한다 — 앞에 두면 금빛 고리가 얼굴을 가로지른다.
        // 그림은 160칸이고 상자는 그 정수배라야 픽셀이 안 깨진다.
        // 2배(320)로 두었더니 3배로 당긴 얼굴(246칸)을 겨우 감싸 고리가 얼굴에 묻혔다.
        // 3배(480)면 얼굴 바깥으로 확실히 퍼진다.
        Image aura = CreateImage("Aura", root, Center, Vector2.zero, new Vector2(480f, 480f),
                                 Color.white);
        aura.raycastTarget = false;
        aura.enabled = false;

        // 손님 자리.
        //
        // 손님은 카운터 "너머"에 서 있어야 한다. 자리의 아래변이 곧 카운터 선이고,
        // 아래에 씌운 마스크가 거기서 몸을 잘라 카운터에 가린 것처럼 보이게 한다.
        //
        // 카운터 선은 배경 그림에서 직접 쟀다. 주문화면 배경.png 는 1671x941 사진인데
        // 나무가 화면 폭을 다 덮기 시작하는 줄이 596 이다. 사진은 판 높이에 맞춰 눌러 담긴다.
        //
        // 숫자를 적어 두지 않고 판 높이에서 계산한다. 예전에 -48 로 박아 두었더니
        // 판이 640x360 에서 960x540 으로 바뀌면서 24칸이 어긋났다. 잘린 몸 아래로 배경이
        // 비쳐 손님이 상 위에 떠 있는 것처럼 보인다.
        //   360 칸이면 -48,  540 칸이면 -72 가 나온다.
        //
        // 얼굴과 몸통을 따로 얹는다. 손님이 바뀔 때마다 CustomerAppearance 가 무작위로 짝짓는다.
        // 그림 여덟 장(얼굴 4 + 몸통 4)으로 열여섯 가지가 나온다.

        /// 사진 941 줄 중 나무가 화면을 덮기 시작하는 줄.
        const float CounterRow = 596f / 941f;

        float CounterLineY = Mathf.Round(DesignResolution.y * (0.5f - CounterRow));

        // 상단바 바로 아래. 판이 커지면 상단바도 위로 따라가므로 같이 계산한다.
        float CustomerTopY = Mathf.Round(DesignResolution.y * 0.5f - 24f - PanelBarHeight * 0.5f - 4f);

        // 어색한 침묵에 지나가는 까마귀. **손님보다 먼저** 만들어야 손님 뒤로 지나간다.
        // 앞으로 지나가면 얼굴을 덮어서, 웃기기 전에 거슬린다.
        Image crow = CreateImage("Crow", root, Center, new Vector2(-600f, 40f),
                                 CrowSize, Color.white);
        crow.raycastTarget = false;
        crow.enabled = false;

        var slot = (RectTransform)CreateGroup("CustomerSlot", root);
        slot.anchorMin = Center;
        slot.anchorMax = Center;
        slot.pivot = Center;
        slot.anchoredPosition = new Vector2(0f, (CustomerTopY + CounterLineY) * 0.5f);
        // 손님 그림이 300칸이라 자리도 그만큼 넓어야 한다. 126 으로 두면 마스크가 좌우를 잘라 낸다.
        slot.sizeDelta = new Vector2(CustomerPortraitSize, CustomerTopY - CounterLineY);

        // 자리 밖으로 나간 부분을 잘라 낸다. 손님을 카운터 선 아래로 내려 두면
        // 여기서 잘려서 카운터 뒤에 서 있는 것처럼 보인다.
        Undo.AddComponent<RectMask2D>(slot.gameObject);

        // 몸통이 먼저(뒤에), 얼굴이 나중(앞에) 그려져야 목이 옷깃에 묻힌다.
        Image bodyImage = CreateImage("Body", slot, Center, Vector2.zero, new Vector2(94f, 118f), Color.white);
        Image headImage = CreateImage("Head", slot, Center, Vector2.zero, new Vector2(72f, 78f), Color.white);
        bodyImage.raycastTarget = false;
        headImage.raycastTarget = false;

        // 리뉴얼된 통짜 손님 그림. 얼굴·몸통보다 나중에 만들어 앞에 그린다.
        Image portrait = CreateImage("Portrait", slot, Center, Vector2.zero,
                                     new Vector2(CustomerPortraitSize, CustomerPortraitSize), Color.white);
        portrait.raycastTarget = false;
        portrait.enabled = false;

        // 갸웃할 때 관자놀이에 맺히는 땀방울. 손님 자리의 자식이라 확대·이동을 같이 탄다.
        // 얼굴 바로 오른쪽 바깥이다 — 얼굴 위에 얹으면 뺨에 붙은 점처럼 보인다.
        Image sweat = CreateImage("Sweat", slot, Center, SweatPos, SweatSize, Color.white);
        sweat.raycastTarget = false;
        sweat.enabled = false;

        var look = Undo.AddComponent<CustomerAppearance>(slot.gameObject);
        SetPrivateReference(look, "bodyImage", bodyImage);
        SetPrivateReference(look, "headImage", headImage);
        SetPrivateReference(look, "portraitImage", portrait);
        SetPrivateArray(look, "bodies", LoadCustomerParts("body"));
        SetPrivateArray(look, "heads", LoadCustomerParts("head"));
        // 인영을 먼저 굽는다. 원본을 읽으려면 isReadable 을 켜야 하고, 그 다시 읽기가
        // 손님 그림을 칸으로 자른 뒤에 일어나면 방금 잡아 둔 스프라이트가 흔들린다.
        SetPrivateArray(look, "silhouettes", LoadCustomerSilhouettes());
        SetPrivateArray(look, "portraits", LoadCustomerPortraits());
        SetPrivateArray(look, "faces", LoadCustomerFaces());

        // 카운터(테이블)를 한 겹 떼어 손님 **위에** 덮는다. Tools/split_counter.py 가 배경에서 잘라 굽는다.
        //
        // 배경 한 장에 카운터까지 들어 있을 때는 손님을 카운터 뒤로 보낼 방법이 없어서,
        // 자리 마스크로 아래를 잘라 뒤에 선 척을 시켰다. 이제는 진짜로 뒤에 선다.
        // 그릇·젓가락은 이 뒤에 만들어 카운터 앞에 놓이게 한다.
        //
        // 그림은 이미 화면 크기(960 폭)라 늘리지 않는다. 배경처럼 Bilinear 로 넣으면
        // 1:1 인데도 한 겹 뭉개진다.
        float counterHeight = CounterLineY + DesignResolution.y * 0.5f;
        Image counter = CreateImage("Counter", root, Center,
                                    new Vector2(0f, CounterLineY - counterHeight * 0.5f),
                                    new Vector2(DesignResolution.x, counterHeight), Color.white,
                                    LoadSprite(ScreenDir + "주문화면 카운터.png"));
        counter.raycastTarget = false;

        // 침묵의 점 셋. 손님보다 나중에 만들어 머리 위에 얹힌다.
        // 까마귀는 손님 뒤라 점 뒤로 지나가고, 점은 까마귀가 지날 때 하나씩 찍힌다.
        Sprite dotSprite = LoadSprite(EtcDir + "침묵점.png");
        var dots = new Image[3];
        for (int i = 0; i < dots.Length; i++)
        {
            dots[i] = CreateImage("SilenceDot" + i, root, Center,
                                  new Vector2((i - 1) * DotGap, DotY), DotSize,
                                  Color.white, dotSprite);
            dots[i].raycastTarget = false;
            dots[i].enabled = false;
        }

        // 손님 앞 카운터에 놓인 김 나는 그릇. 카운터보다 나중에 만들어 그 앞에 그린다.
        // 가운데에 두면 손님과 한 축에 서서 "이 손님 앞에 놓인 그릇"으로 읽힌다.
        //
        // 그릇과 접지 그림자를 한 자리에 묶는다. 그릇을 낼 때 둘 다 같이 나타나야 하는데,
        // OrderScreenUI 가 쥐는 servedBowl 은 오브젝트 하나라 따로 두면 그림자만 남는다.
        // 묶어 두면 이 자리 하나를 껐다 켜는 것으로 끝난다.
        var servedBowl = (RectTransform)CreateGroup("ServedBowl", root);
        servedBowl.anchorMin = Center;
        servedBowl.anchorMax = Center;
        servedBowl.pivot = Center;
        servedBowl.anchoredPosition = new Vector2(0f, CustomerBowlY);
        servedBowl.sizeDelta = new Vector2(CustomerBowlSize, CustomerBowlSize);

        // 그림자가 먼저(뒤에), 그릇이 나중(앞에). 그림자는 그릇과 **별개 오브젝트**다 —
        // 붙여 두면 국물을 마실 때 그림자까지 같이 떠올라 그릇이 공중에 선 것으로 보인다.
        Image bowlShadow = CreateImage("Shadow", servedBowl, Center,
                                       new Vector2(0f, (BowlShadowPivotY - 0.5f) * CustomerBowlSize),
                                       new Vector2(CustomerBowlSize, CustomerBowlSize), Color.white,
                                       LoadSprite(EtcDir + "그릇그림자.png"));
        bowlShadow.rectTransform.pivot = new Vector2(0.5f, BowlShadowPivotY);
        bowlShadow.raycastTarget = false;

        Image customerBowl = CreateImage("CustomerBowl", servedBowl, Center, Vector2.zero,
                                         new Vector2(CustomerBowlSize, CustomerBowlSize), Color.white);
        customerBowl.raycastTarget = false;

        // 라멘을 내기 전에는 없다. 주문받는 동안 그릇이 놓여 있으면 이미 준 것처럼 보인다.
        servedBowl.gameObject.SetActive(false);

        // 시험용 — 손님 앞 그릇을 조리 화면에서 실제로 만든 그릇으로 바꾼다.
        // 조리 화면 그릇은 여기서 못 찾는다(다른 함수가 만든다). 아래쪽 배선에서 꽂아 준다.
        var mirror = Undo.AddComponent<ServedBowlMirror>(servedBowl.gameObject);
        SetPrivateReference(mirror, "stockBowl", customerBowl);

        // 그릇은 한 장, 김은 여덟 장. 예전에는 둘이 한 시트(손님그릇.png)에 같이 그려져
        // 있었는데 그러면 두 가지가 안 된다(Tools/split_customer_bowl.py 가 떼어냈다).
        //
        //   * 먹기 전에는 컷신이 안 돌아 **김이 정지화면**이었다. 뜨거운 그릇인데.
        //   * 다 먹을 때 **김만** 옅게 못 한다 — 알파를 내리면 그릇까지 사라진다.
        customerBowl.sprite = LoadSprite(EtcDir + "손님그릇_바닥.png");

        Image bowlSteam = CreateImage("Steam", customerBowl.transform, Center, Vector2.zero,
                                      new Vector2(CustomerBowlSize, CustomerBowlSize), Color.white);
        bowlSteam.raycastTarget = false;

        var bowlLoop = Undo.AddComponent<SpriteLoop>(bowlSteam.gameObject);
        bowlLoop.frames = LoadSpriteSheet(EtcDir + "손님그릇_김.png",
                                          (int)CustomerBowlSize, (int)CustomerBowlSize);
        bowlLoop.fps = 6f;   // 김이 천천히 피어오른다. 늘 돈다 — 끄지 않는다
        if (bowlLoop.frames.Length > 0) bowlSteam.sprite = bowlLoop.frames[0];

        // 손님이 걸어오는 소리. 오디오 파일이 없어 파형을 코드로 만들어 쓴다.
        // AudioSource 는 RequireComponent 가 같이 붙여 준다.
        Undo.AddComponent<Footsteps>(slot.gameObject);

        // 손님 대화창.
        //
        // 크기는 말 길이에 따라 변한다(OrderScreenUI.LayoutDialogue). 그래서 어느 모서리를
        // 고정하느냐가 중요하다.
        //
        // 오른쪽 위를 고정한다(피벗 1,1). 꼬리가 오른쪽 변에 달려 있어서, 여기를 잡아야
        // 꼬리가 늘 같은 자리에 선다. 왼쪽 위를 잡고 있었더니 짧은 대사에서 상자가 줄면서
        // 오른쪽 변이 왼쪽으로 딸려 가, 꼬리가 손님에게서 130칸씩 멀어졌다.
        // 상자는 이제 왼쪽으로만 자란다.
        //
        // 자리는 손님 머리에서 조금 떨어진 지점이다. 판 한가운데가 손님이라 Center 기준으로
        // 잡는다 — 판 크기가 바뀌어도 손님과의 거리가 그대로 유지된다.
        // 꼬리 끝이 오른쪽 변에서 23칸 더 나가므로, 오른쪽 변을 -67 에 두면 꼬리 끝이 -44 다.
        // 손님 머리 왼쪽 끝이 -36 언저리라 8칸쯤 띄운 셈이다.
        // 세로 자리는 꼬리가 손님 귀와 나란히 서도록 잡는다.
        //
        // 손님 그림을 재 보니 머리끝에서 목까지가 170칸이고 가장 넓은 곳이 90칸이었다.
        // 귀는 그 사이, 머리끝에서 100칸쯤이다. 그림 맨 위가 화면 y 198 이므로 귀는 y 98.
        // 꼬리는 상자 윗변에서 BubbleTailTop(24) 내려가 15칸을 차지하니, 꼬리 한가운데를
        // 귀에 맞추려면 상자 윗변이 98 + 24 + 7.5 = 130 이다.
        Image bubble = CreateImage("Bubble", root, Center, new Vector2(-67f, BubbleTopY),
                                   new Vector2(BubbleMaxTextWidth + BubblePaddingX * 2f, BubbleHeight),
                                   Color.white, SpeechBubbleBodySprite());
        bubble.rectTransform.pivot = new Vector2(1f, 1f);

        // 9-슬라이스로 그린다. 배율 1이라 테두리가 원본 그대로(얇게) 남는다.
        bubble.type = Image.Type.Sliced;
        bubble.pixelsPerUnitMultiplier = 1f;

        // 꼬리. 본문과 같은 배율(1배)로 둔다. 3배로 띄웠더니 꼬리 외곽선만 3픽셀이라
        // 본문의 1픽셀 테두리와 따로 놀았다. 크기는 그림을 다시 구워 키운다(BubbleTailScale).
        //
        // x 를 -1 로 둔다. 꼬리 왼쪽 열이 본문 테두리 위에 얹히는데 그 열에는 외곽선을
        // 치지 않아, 테두리를 덮어 지우면서 거기가 입이 된다.
        //
        // 세로는 "윗변에서 잰 거리"로 고정한다. 상자 윗변이 고정이라 꼬리도 고정된다.
        //
        // 한때 세로 한가운데(0.5)에 걸어 둔 적이 있다. 짧은 대사에서 꼬리가 상자 밖으로
        // 삐져나오는 것을 막으려는 것이었는데, 그러면 상자가 자랄 때마다 가운데가 움직여
        // 꼬리가 같이 오르내린다. 꼬리는 손님을 가리키는 것이라 손님이 안 움직이면
        // 꼬리도 안 움직여야 한다.
        //
        // 삐져나오는 문제는 거리를 줄여서 푼다. 가장 작은 상자(한 줄 = 43칸)에도
        // 꼬리(15칸)가 다 들어가야 한다.  BubbleTailTop 24 + 15 = 39 <= 43
        var tail = CreateImage("BubbleTail", bubble.transform, new Vector2(1f, 1f),
                               new Vector2(-1f, -BubbleTailTop),
                               new Vector2(BubbleTailW * BubbleTailScale, BubbleTailH * BubbleTailScale),
                               Color.white, SpeechBubbleTailSprite());
        tail.rectTransform.pivot = new Vector2(0f, 1f);
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
        viewport.anchoredPosition = new Vector2(BubblePaddingX, -BubblePaddingY);
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

        // 넘기기 버튼. 말풍선 "바깥" 아래에 매단다. 상자 안은 글자만 쓴다.
        //
        // 자리를 상자 "아래변"에 건다. 오른쪽 끝을 맞추는 것은 꼬리와 같은 쪽이라
        // 시선이 한 줄로 떨어지기 때문이다.
        //
        // 예전에는 윗변에 걸고 BubbleHeight 만큼 내렸다. 상자 높이가 마디마다 달라져도
        // 버튼이 위아래로 안 뛰게 하려던 것인데, BubbleHeight 가 "가장 긴 마디 한 개"
        // 기준이라 실제 상자보다 훨씬 낮았다. 대사는 클릭할 때마다 줄이 쌓여서
        // 상자가 그보다 한참 커지고, 그러면 버튼이 상자 안으로 들어가 글자를 가렸다.
        // 아래변에 걸면 줄이 몇이든 늘 글 밑에 선다.
        //
        // 글자를 "▶"과 "넵"으로 줄여 버튼도 같이 작아졌다. 예전 112x30 은 상자의 절반을 먹었다.
        Image startImage = CreateImage("StartButton", bubble.transform, new Vector2(1f, 0f),
                                       new Vector2(0f, -BubbleButtonGap),
                                       new Vector2(56f, 36f), Hex("#7BA7C7"),
                                       LoadSlicedSprite(UiDir + "TextBox.png", new Vector4(8f, 8f, 8f, 8f)));
        startImage.rectTransform.pivot = new Vector2(1f, 1f);
        startImage.type = Image.Type.Sliced;
        startImage.pixelsPerUnitMultiplier = 1f;

        var start = Undo.AddComponent<Button>(startImage.gameObject);
        start.targetGraphic = startImage;
        StyleButton(start);
        var startLabel = CreateTmpText("Label", startImage.transform, Center, Vector2.zero,
                                       new Vector2(30f, 18f), "▶", DialogueFontSize, tmpFont);
        startLabel.color = Color.white;

        // 조리 화면 상단바와 같은 모양으로 맞춘다. 두 화면을 오가는데 같은 정보가 다른 판에
        // 다른 글로 떠 있으면 다른 게임의 UI 처럼 보인다. 크기·자리·글까지 그쪽을 따른다.
        Image dayPanel = CreateImage("DayTimePanel", root, TopLeft, new Vector2(255f, -24f),
                                     new Vector2(200f, PanelBarHeight), Color.white, TimeBarSprite(), PanelScale);
        Image dayIcon = AttachPanelIcon(dayPanel, TimeIconSprite(), new Vector2(40f, 36f));
        var dayClock = dayIcon != null ? Undo.AddComponent<DayClockIcon>(dayIcon.gameObject) : null;
        var dayTime = CreateTmpText("DayTimeText", dayPanel.transform, Center, Vector2.zero,
                                    new Vector2(174f, 38f), "1일차  17:00", TextHead, tmpFont);

        // 조리 화면 상단바와 같은 값이다(판 290 / 상자 254 / TextHead 16 / 글자를 8 오른쪽으로).
        // 목표까지 싣느라 24 로는 366칸이 되어 안 들어간다 — 그쪽과 같은 이유, 같은 해법이다.
        Image revenuePanel = CreateImage("RevenuePanel", root, TopRight, new Vector2(-201f, -24f),
                                         new Vector2(290f, PanelBarHeight), Color.white, MoneyBarSprite(), PanelScale);
        AttachPanelIcon(revenuePanel, MoneyIconSprite(), new Vector2(36f, 28f));
        var revenue = CreateTmpText("RevenueText", revenuePanel.transform, Center, new Vector2(8f, 0f),
                                    new Vector2(254f, 38f), "금일 수익 : 0 / 0₩", TextHead, tmpFont);

        root.gameObject.SetActive(false);

        // ── 먹는 연출용 판들 ──────────────────────────────────────────
        //
        // 여기서 만드는 것은 전부 다른 것들 "위에" 덮여야 하므로 맨 마지막에 만든다.
        // 유니티 UI 는 나중에 만든 형제가 위에 그려진다.

        // 반짝임이 도는 자리. 조각은 EatingCutscene 이 필요할 때 만들어 넣는다.
        // 손님 윗몸 언저리에 두어야 얼굴 주변에서 반짝인다.
        var sparkleRoot = (RectTransform)CreateGroup("Sparkles", root);
        sparkleRoot.anchorMin = Center;
        sparkleRoot.anchorMax = Center;
        sparkleRoot.pivot = Center;
        // 따봉 둘레에 흩어진다. (0, 40) 이던 시절에는 손님 몸 한가운데라, 엄지와 아무 상관
        // 없는 자리에서 흰 알갱이가 솟는 것처럼 보였다. 자리와 크기를 따봉에 맞춘다.
        sparkleRoot.anchoredPosition = new Vector2(130f, 10f);
        sparkleRoot.sizeDelta = new Vector2(150f, 150f);


        // 따봉. 그림이 40칸이라 그대로 두면 너무 작다. 2배(80)로 키운다.
        // 손님 왼손 자리. 화면에서는 손님 오른쪽이고, 말풍선(판 왼쪽 위)과도 안 겹친다.
        //
        // (72, -16) 이었다. 손님 앞 그릇을 카운터 맨 위 판으로 49칸 올리면서 그릇이
        // 화면 x -83~82 · y -109~45 를 차지하게 되어 따봉이 거기 묻혔다.
        // 오른쪽으로 빼서 그릇 오른쪽 끝(82)을 넘긴다 — 130 이면 왼쪽 변이 90 이라 8칸 뜬다.
        Vector2 thumbSpot = new Vector2(130f, 10f);

        // 따봉 뒤에서 도는 아우라. 따봉보다 먼저 만들어 뒤에 깔린다.
        Image thumbAura = CreateImage("ThumbAura", root, Center, thumbSpot,
                                      new Vector2(160f, 160f), Color.white, AuraSprite());
        thumbAura.raycastTarget = false;
        thumbAura.enabled = false;

        Image thumb = CreateImage("Thumb", root, Center, thumbSpot,
                                  new Vector2(80f, 80f), Color.white,
                                  LoadSprite(EtcDir + "따봉.png"));
        thumb.raycastTarget = false;
        thumb.enabled = false;

        // 둘을 카운터 **뒤**로 보낸다. 따봉이 테이블 아래에서 솟아오르는 연출이라,
        // 앞에 있으면 올라오는 내내 허공에 떠서 보인다. 뒤에 두면 카운터가 가려 준다.
        // 만드는 순서를 옮기는 대신 형제 차례만 바꾼다 — 아래쪽 배선이 그대로 살아 있어야 한다.
        int behindCounter = counter.transform.GetSiblingIndex();
        thumbAura.transform.SetSiblingIndex(behindCounter);
        thumb.transform.SetSiblingIndex(behindCounter + 1);

        // 그림은 오른손 모양으로 그려져 있어서 좌우를 뒤집어 왼손으로 만든다.
        // 뒤집기는 칸이 그대로 맞바뀌는 것이라 회전과 달리 픽셀이 안 깨진다.
        thumb.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        // 번쩍임. 번개 그림이 없어 흰 판으로 대신한다. 알파는 연출이 쥔다.
        //
        // 시네마틱 바보다 먼저 만든다. 나중에 만들면 바 위에 덮여, 번개가 칠 때마다
        // 검은 바까지 하얗게 떠서 깜빡이는 것처럼 보인다. 바는 늘 검어야 한다.
        Image flash = CreateImage("Flash", root, Center, Vector2.zero, ScreenCover,
                                  new Color(1f, 1f, 1f, 0f));
        flash.raycastTarget = false;
        flash.enabled = false;

        // 시네마틱 검은 바. 높이 0으로 두고 EatingCutscene 이 늘렸다 줄인다.
        // 폭은 판(640)이 아니라 화면 전체로 잡는다. 16:9가 아닌 창에서 판 옆이 비어 보이면
        // 바가 거기서 끊겨 시네마틱으로 안 보인다.
        Image topBar = CreateImage("LetterboxTop", root, TopCenter, Vector2.zero,
                                   new Vector2(ScreenCover.x, 0f), Color.black);
        topBar.rectTransform.pivot = new Vector2(0.5f, 1f);
        topBar.raycastTarget = false;

        Image bottomBar = CreateImage("LetterboxBottom", root, new Vector2(0.5f, 0f), Vector2.zero,
                                      new Vector2(ScreenCover.x, 0f), Color.black);
        bottomBar.rectTransform.pivot = new Vector2(0.5f, 0f);
        bottomBar.raycastTarget = false;

        var cutscene = Undo.AddComponent<EatingCutscene>(root.gameObject);
        SetPrivateReference(cutscene, "topBar", topBar.rectTransform);
        SetPrivateReference(cutscene, "bottomBar", bottomBar.rectTransform);
        SetPrivateReference(cutscene, "flash", flash);
        SetPrivateReference(cutscene, "sparkleRoot", sparkleRoot);
        SetPrivateReference(cutscene, "cosmos", cosmos);
        SetPrivateReference(cutscene, "dim", dim);
        SetPrivateReference(cutscene, "bolt", bolt);
        SetPrivateReference(cutscene, "aura", aura);
        SetPrivateReference(cutscene, "auraGlow", auraGlow);
        SetPrivateArray(cutscene, "auraFrames", LoadSpriteSheet(EtcDir + "오우라.png", 160, 160));
        SetPrivateReference(cutscene, "thumb", thumb);
        SetPrivateReference(cutscene, "thumbAura", thumbAura);

        // 후루룩과 천둥. AudioSource 는 RequireComponent 가 같이 붙여 준다.
        SetPrivateReference(cutscene, "sfx", Undo.AddComponent<CutsceneSfx>(root.gameObject));
        SetPrivateReference(cutscene, "customerSlot", slot);
        // 확대는 통짜 손님 그림을 기준으로 잡는다. 얼굴이 그 그림 안에 들어 있어서,
        // 예전 얼굴 판을 가리키면 크기도 자리도 안 맞는다. 눈높이는 eyeHeightRatio 가 정한다.
        SetPrivateReference(cutscene, "customerHead", portrait.rectTransform);

        // 국물을 마실 때 들어 올릴 그릇. 자리(ServedBowl) 안에서만 오르내린다.
        SetPrivateReference(cutscene, "customerBowl", customerBowl.rectTransform);

        // 그릇이 뜨는 동안 카운터에 남아 좁아지고 옅어지는 그림자.
        SetPrivateReference(cutscene, "customerBowlShadow", bowlShadow);

        // 먹는 동안 눈을 감기고 감동하는 순간 뜨게 하려면 손님 그림을 직접 쥐어야 한다.
        SetPrivateReference(cutscene, "customerAppearance", look);

        // 갸웃할 때 쓰는 땀방울. 맺힘 / 흘러내림 두 칸이다.
        SetPrivateReference(cutscene, "sweat", sweat);
        SetPrivateArray(cutscene, "sweatFrames",
                        LoadSpriteSheet(EtcDir + "땀방울.png", (int)SweatSize.x, (int)SweatSize.y));

        // 어색한 침묵에 지나가는 까마귀와, 그가 지나며 찍고 가는 점 셋.
        SetPrivateReference(cutscene, "crow", crow);
        SetPrivateArray(cutscene, "crowFrames",
                        LoadSpriteSheet(EtcDir + "까마귀.png", (int)CrowSize.x, (int)CrowSize.y));
        SetPrivateArray(cutscene, "silenceDots", dots);

        return new OrderScreenRefs
        {
            Root = root.gameObject,
            DayTime = dayTime,
            DayClock = dayClock,
            Revenue = revenue,
            Dialogue = dialogue,
            DialogueViewport = viewport,
            Backdrop = night,
            Start = start,
            StartImage = startImage,
            StartLabel = startLabel,
            Look = look,
            Cutscene = cutscene
        };
    }

    /// <summary>5일 영업이 끝났을 때 뜨는 최종 성적표. 정산 팝업과 같은 짜임새다.</summary>
    private static FinalPopupRefs BuildFinalPopup(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform root = CreateGroup("FinalResultPopup", canvas);
        LiftPopup(root.gameObject);

        var backdrop = CreateImage("Backdrop", root, Center, Vector2.zero, ScreenCover, new Color(0f, 0f, 0f, 0.7f));
        backdrop.raycastTarget = true;

        // 판은 **사용자가 다듬어 온 그림**이다. `Tools/make_final_board.py` 가 받은 원본에서
        // 숫자 석 줄과 버튼 자리를 빈 종이로 덮고 화면 크기(300x345)로 줄여 굽는다.
        //
        // 예전에는 크림색 사각판(#FFF8E7)에 초록 버튼이었다. 하루 정산은 받아 온 영수증 그림인데
        // **게임의 마지막 화면**만 코드로 그린 판이라, 5일을 버틴 끝에 뜨는 화면이 제일 허술했다.
        Image panel = CreateImage("Panel", root, Center, Vector2.zero, new Vector2(300f, 345f),
                                  Color.white, LoadSprite(GeneratedDir + "최종결산판.png"));

        // **제목은 만들지 않는다.** 「5일 영업 종료」가 판 그림에 그려져 있다.
        // 늘 같은 말이라 구워도 되고, FinalResultUI 는 title 이 비어 있어도 그냥 지나간다.
        //
        // 아래 자리는 **받은 그림에서 글자가 있던 자리를 그대로 환산한 값**이다
        // (원본 y 565 / 685 / 805 / 1045, 배율 0.2933). 눈대중으로 잡으면 점선·톱니와 어긋난다.
        var revenue = CreateTmpText("RevenueText", panel.transform, Center, new Vector2(0f, 31f),
                                    new Vector2(260f, 20f), "누적 매출 : 0원", TextBody, tmpFont);
        var accuracy = CreateTmpText("AccuracyText", panel.transform, Center, new Vector2(0f, -4f),
                                     new Vector2(260f, 20f), "평균 정확도 : 0.0%", TextBody, tmpFont);
        var perfect = CreateTmpText("PerfectText", panel.transform, Center, new Vector2(0f, -40f),
                                    new Vector2(260f, 20f), "완벽한 한 그릇 : 0 / 0건", TextBody, tmpFont);

        // 버튼은 정산 팝업이 쓰는 그림을 그대로 쓴다. **글자가 그림에 박혀 있어** 따로 얹지 않는다.
        // 누르면 하는 일은 그대로 다시 시작이다 — 글자만 「확인」이다.
        Image restartImage = CreateImage("RestartButton", panel.transform, Center, new Vector2(0f, -110f),
                                         new Vector2(224f, 75f), Color.white,
                                         LoadPhotoSprite(UiDir + "버튼_확인.png"));
        var restart = Undo.AddComponent<Button>(restartImage.gameObject);
        restart.targetGraphic = restartImage;
        StyleButton(restart);

        root.gameObject.SetActive(false);

        return new FinalPopupRefs
        {
            Root = root.gameObject,
            Title = null,             // 제목은 판 그림에 구워져 있다
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
        TMP_FontAsset slotFont = EnsureTmpFont();

        foreach (SlotDef def in Slots)
        {
            // 돌리는 통은 시트로 잘라 온다. LoadSprite 를 먼저 부르면 임포터가 Single 로 바뀌어
            // 그다음 LoadSpriteSheet 가 다시 잘라야 하므로, 처음부터 갈라 둔다.
            Sprite[] loopFrames = def.LoopCell > 0
                ? LoadSpriteSheet(def.BinPath, def.LoopCell, def.LoopCell)
                : null;

            // 자르기 영역이 있으면 그 부분만 떼어 쓴다. 재료통은 투명 여백을 잘라야 한 줄에 들어간다.
            Sprite binSprite = loopFrames != null && loopFrames.Length > 0
                ? loopFrames[0]
                : def.CropRect.width > 0f
                    ? LoadCroppedSprite(def.BinPath, def.Type + (def.IdSuffix ?? "") + "Bin", def.CropRect, Vector4.zero)
                    : LoadSprite(def.BinPath);

            Image bin = CreateImage("Slot_" + def.Type + (def.IdSuffix ?? ""), parent, def.Anchor, def.Pos, def.Size,
                                    Color.white, binSprite);
            bin.preserveAspect = true;

            // 마우스 판정을 그림 모양에 맞추려면 원본을 런타임에 읽을 수 있어야 한다.
            // 안 켜 두면 마우스를 올리는 순간 "Texture is not readable" 예외가 난다.
            //
            // 판정값 자체는 여기서 못 넣는다. uGUI 의 alphaHitTestMinimumThreshold 는
            // 직렬화되지 않는 필드라(Image.cs 주석 "Not serialized until we support
            // read-enabled sprites better") 씬에 저장되지 않고 플레이할 때 0 으로 돌아온다.
            // 실제로 빌드해 봤더니 씬 파일에 값이 하나도 안 남았다. 그래서 SlotHover 가
            // 실행될 때 직접 넣는다.
            ReadableTexture(def.BinPath);

            // 튜토리얼 안내 테두리. 통과 같은 상자에 겹쳐 두고 자기 차례일 때만 켜진다.
            BuildTutorialOutline(bin, def);

            if (loopFrames != null && loopFrames.Length > 1)
            {
                var loop = Undo.AddComponent<SpriteLoop>(bin.gameObject);
                loop.frames = loopFrames;
            }

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

            // 마우스를 올렸을 때 덧씌우는 흰 막. 통과 같은 상자에 겹쳐 두고 호버 때만 켜진다.
            //
            // 통 그림을 그대로 쓰면 안 된다. 흰색으로 물들여 봐야 흰색 곱하기는 색을 안 바꿔서
            // 같은 그림이 한 번 더 그려질 뿐이다(실제로 그렇게 만들었다가 아무 변화가 없었다).
            // 모양만 같고 속이 하얀 실루엣을 따로 구워 쓴다.
            Rect fillCrop = def.CropRect;
            if (def.LoopCell > 0)
            {
                Texture2D sheet = ReadableTexture(def.BinPath);
                fillCrop = sheet != null
                    ? new Rect(0f, sheet.height - def.LoopCell, def.LoopCell, def.LoopCell)
                    : default;
            }

            Image glow = CreateImage("HoverGlow", bin.transform, Center, Vector2.zero, def.Size,
                                     Color.white,
                                     SilhouetteSprite(def.BinPath, def.Type + (def.IdSuffix ?? ""), fillCrop));
            glow.preserveAspect = bin.preserveAspect;
            glow.raycastTarget = false;
            glow.enabled = false;

            // 통이 커지거나 그림이 바뀌어도 막이 따라가도록 상자에 붙여 둔다.
            glow.rectTransform.anchorMin = Vector2.zero;
            glow.rectTransform.anchorMax = Vector2.one;
            glow.rectTransform.offsetMin = Vector2.zero;
            glow.rectTransform.offsetMax = Vector2.zero;

            hover.highlight = glow;

            // 이름은 통 안에 쓴다. 흰 막 위에 얹으므로 막보다 나중에 만든다.
            //
            // 글자 상자를 통보다 넓게 잡는다. 시치미 통은 64칸인데 「고추가루」는 24짜리 글자로
            // 96칸이라, 통 폭에 맞추면 두 줄로 접혀 통을 다 덮는다. 넘치더라도 한 줄로 두고
            // 가운데를 통에 맞추는 편이 읽힌다.
            var slotName = CreateTmpText("HoverName", bin.transform, Center, Vector2.zero,
                                         new Vector2(Mathf.Max(def.Size.x, 140f), 30f),
                                         def.Label, TextTitle, slotFont);
            slotName.color = ResultInkColor;
            slotName.raycastTarget = false;
            slotName.textWrappingMode = TextWrappingModes.NoWrap;
            slotName.enabled = false;

            hover.nameLabel = slotName;

            // 이름판은 뺐다. 통 그림만으로 무엇인지 읽히는지 먼저 보기로 했다.
            // 되살리려면 이 줄을 살린다:  CreateSlotLabel(bin.transform, def, font);

            // 아직 안 들어온 재료통에는 자물쇠를 얹는다. 1일차에 잠기는 통에만 붙이면 된다 —
            // 첫날부터 열려 있는 통은 영영 안 잠긴다. 어느 날 열리는지는 IngredientUnlock 이 정한다.
            if (IngredientUnlock.IsLocked(def.Type, 1)) BuildSlotLock(bin, hover, def);

            // 돌아가는 통(육수 냄비)은 알파 판정을 쓰지 않는다.
            //
            // 한 장에 네 칸이 든 시트를 SpriteLoop 가 매 프레임 갈아 끼우는데, 그 상태에서
            // 알파를 찍으면 판정이 실제 냄비보다 한참 작게 잡혔다. 냄비 그림은 어차피
            // 128칸 중 124x120 을 채워서 상자 전체를 판정으로 써도 그림과 거의 같다.
            if (def.LoopCell > 0) hover.hitAlpha = 0f;

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
        // 국자는 국물 색마다 시트가 따로다. 시오·향미유·육수가 금색 한 장을 나눠 쓴다.
        // 마지막 칸(35번)은 빈 칸이라 잘라 낸다. CookingCursor 가 시트 끝 칸을 "빈 국자"로 쓰므로
        // 빈 칸이 남아 있으면 국자가 통째로 사라진 것처럼 보인다.
        cursor.ladleShioFrames = LadleSheet("국자_시오향미유.png");
        cursor.ladleShoyuFrames = LadleSheet("국자_쇼유.png");
        cursor.ladleTonkotsuFrames = LadleSheet("국자_돈코츠.png");

        // 면 소쿠리. 터는 것과 쏟는 것 두 벌씩이고 각각 40칸이다.
        cursor.noodleThinDrainFrames = LoadSpriteSheet(CookDir + "면털기_얇은면.png", 128, 128);
        cursor.noodleThickDrainFrames = LoadSpriteSheet(CookDir + "면털기_굵은면.png", 128, 128);
        cursor.noodleThinPourFrames = NoodlePourSheet("면붓기_얇은면.png");
        cursor.noodleThickPourFrames = NoodlePourSheet("면붓기_굵은면.png");

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

        #pragma warning disable 0618 // spritesheet 폐기 예고 — LoadIconSprites 의 설명 참조
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
        #pragma warning restore 0618

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

    /// <summary>
    /// 그릇 위에 뜨는 튜토리얼 안내 한 줄.
    ///
    /// 그릇 윗변 언저리에 둔다. 붙이면 국물에 글자가 걸치고, 더 올리면 상단바에 닿는다.
    /// 판은 재료통 이름표와 같은 TextBox.png 라 화면에서 겉돌지 않는다.
    /// </summary>
    private static void BuildTutorialPrompt(Transform canvas, RectTransform bowl)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        // 크기는 TutorialPrompt 가 글에 맞춰 다시 잡는다. 여기 값은 첫 한 프레임짜리다.
        //
        // 피벗을 아래변에 둔다. 판이 커질 때 위로만 자라야 그릇과의 간격이 그대로다.
        // 가운데 피벗이면 위아래로 같이 자라서 두 줄짜리 안내가 그릇을 덮는다.
        //
        // 그릇 윗변에서 12 띄웠더니 두 줄짜리 안내가 위로 자라면서 마무리 버튼에 딱 닿았다.
        // 그릇 쪽으로 28 내린다. 그릇 그림은 위쪽이 투명 여백이라 글자가 국물에 안 걸린다.
        float y = bowl.anchoredPosition.y + bowl.sizeDelta.y * 0.5f - 16f;

        // 색은 그림이 쥔다(반투명이라 알파까지 그림에 있다). 여기서 곱하면 두 번 어두워진다.
        Image panel = CreateImage("TutorialPrompt", canvas, Center, new Vector2(0f, y),
                                  new Vector2(420f, 48f), Color.white, TutorialPanelSprite());
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 1f;
        panel.raycastTarget = false;
        panel.rectTransform.pivot = new Vector2(0.5f, 0f);
        panel.rectTransform.anchoredPosition = new Vector2(0f, y);

        var label = CreateTmpText("Label", panel.transform, Center, Vector2.zero,
                                  new Vector2(404f, 40f), "", TextBody, tmpFont);
        label.color = DarkPanelInkColor;
        label.raycastTarget = false;

        var promptCanvas = Undo.AddComponent<Canvas>(panel.gameObject);
        promptCanvas.overrideSorting = true;
        promptCanvas.sortingOrder = TutorialPromptOrder;

        var prompt = Undo.AddComponent<TutorialPrompt>(panel.gameObject);
        SetPrivateReference(prompt, "label", label);
        SetPrivateReference(prompt, "panel", panel);
    }

    private static Bowl BuildBowl(Transform canvas)
    {
        // 상단바 아래와 면 튀김기 위 사이의 한가운데. 어느 한쪽에 치우치면 공중에 뜬 것처럼 보인다.
        Image bowl = CreateImage("Bowl", canvas, Center, new Vector2(0f, 40f), BowlSize,
                                 Color.white, LoadSprite(BowlDir + "빈그릇.png"));
        bowl.preserveAspect = true;

        Bowl component = Undo.AddComponent<Bowl>(bowl.gameObject);
        component.emptyBowlSprite = LoadSprite(BowlDir + "빈그릇.png");

        // 튜토리얼 안내 한 줄. 그릇 바로 위에 띄운다.
        // 그릇의 자식으로 달지 않는다 — 제출할 때 그릇이 끌려가면 문구도 같이 끌려간다.
        BuildTutorialPrompt(canvas, bowl.rectTransform);

        // 타래 종류마다 8프레임 시트가 한 장씩(4열 x 2행).
        // 0~3 타래 / 4~6 육수 / 7 면. 재생 길이는 Bowl.PourSeconds(국자 붓기와 같은 길이)로 정해진다.
        component.shioFrames = LoadSpriteSheet(BowlDir + "Sio_Ani.png", 128, 128);
        component.shoyuFrames = LoadSpriteSheet(BowlDir + "Syo_Ani.png", 128, 128);
        component.tonkotsuFrames = LoadSpriteSheet(BowlDir + "Don_Ani.png", 128, 128);

        // 토핑을 올렸을 때 국물이 찰랑이는 16장. 한 바퀴만 돌고 붓기 시트의 면 프레임으로 돌아간다.
        component.shioToppingFrames = LoadSpriteSheet(BowlDir + "Sio_Topping.png", 128, 128);
        component.shoyuToppingFrames = LoadSpriteSheet(BowlDir + "Syo_Topping.png", 128, 128);
        component.tonkotsuToppingFrames = LoadSpriteSheet(BowlDir + "Don_Topping.png", 128, 128);

        // 그릇에 얹는 재료 30칸. 자리마다 기울기와 국물에 잠긴 깊이가 구워져 있다.
        // 칸 순서는 Bowl.Layouts 의 SheetStart 와 맞아야 한다. 자리를 옮기면 시트를 다시 구울 것.
        component.toppingFrames = LoadSpriteSheet(BowlDir + "토핑배치.png", 50, 50);

        // 드래그 중에 레이캐스트를 통과시키려면 CanvasGroup이 필요하다.
        // 없으면 그릇 자신이 SubmitZone을 가려서 제출이 영영 안 된다.
        Undo.AddComponent<CanvasGroup>(bowl.gameObject);

        // 그릇 안 재료 그림이 들어갈 자리. Bowl이 런타임에 여기로 넣는다.
        Transform contents = CreateGroup("Contents", bowl.transform);

        if (ClipUnderBroth) AttachBrothClip(contents);

        // 그릇 위로 피어오르는 김. 재료(Contents)보다 나중에 만들어 그 앞에 그린다.
        //
        // **국물 수면이 아니라 그릇 위쪽에 둔다.** 아래로 내리면 김이 재료를 덮어 라멘이
        // 뿌예 보인다 — 국물 위에 안개가 낀 것이지 피어오르는 김이 아니다. 여기서는
        // 김 아랫단이 그릇 먼 테두리에 걸치고 나머지가 나무 카운터를 배경으로 올라간다.
        // 세 자리(수면·테두리·그 위)를 다 찍어 보고 고른 값이다.
        Image steam = CreateImage("Steam", bowl.transform, Center,
                                  new Vector2(0f, BowlSize.y * 0.5f + BowlSteamRise),
                                  BowlSteamSize, Color.white);
        steam.raycastTarget = false;
        steam.enabled = false;                 // 육수가 들어와야 켜진다(Bowl.ShowSteam)

        // 자르는 것은 **원본 한 칸**이다. 그리는 크기(BowlSteamSize)를 넣으면 시트가 엉뚱하게 잘린다.
        var steamLoop = Undo.AddComponent<SpriteLoop>(steam.gameObject);
        steamLoop.frames = LoadSpriteSheet(CookDir + "그릇김.png",
                                           (int)BowlSteamFrame.x, (int)BowlSteamFrame.y);
        steamLoop.fps = BowlSteamFps;
        if (steamLoop.frames.Length > 0) steam.sprite = steamLoop.frames[0];

        SetPrivateReference(component, "steam", steamLoop);

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
    /// <summary>
    /// 시치미·향미유 개수 배지. 그릇 오른쪽 위에 "아이콘 X N" 두 줄.
    ///
    /// 이 둘만 세는 이유는 그릇에 그림이 안 올라가서다. 나머지 재료는 그릇에 쌓이는 게
    /// 보이므로 굳이 숫자를 붙이지 않는다.
    /// </summary>
    private static SeasoningBadges BuildSeasoningBadges(Transform canvas)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Transform group = CreateGroup("SeasoningBadges", canvas);
        var badges = Undo.AddComponent<SeasoningBadges>(group.gameObject);

        badges.flavorOil = BuildSeasoningRow(group, tmpFont, "FlavorOil", "향미유 배지.png", BadgeTopY);
        badges.chiliPowder = BuildSeasoningRow(group, tmpFont, "ChiliPowder", "시치미 배지.png",
                                               BadgeTopY - BadgeRowGap);
        return badges;
    }

    /// <summary>
    /// 글자 뒤에 검은 복사본 여덟 장을 한 칸씩 밀어 깔아 외곽선을 만든다.
    ///
    /// 복사본을 원본보다 먼저 만든 뒤 원본을 맨 앞으로 보낸다. uGUI 는 만든 순서대로 그려서
    /// 나중 것이 위에 온다. 순서를 안 맞추면 검은 글자가 흰 글자를 덮는다.
    ///
    /// 미는 거리는 한 칸이다. 캔버스 한 칸이 원본 그림 한 픽셀이라 정확히 1픽셀 테두리가 된다.
    /// </summary>
    /// <summary>
    /// 글자 뒤에 검은 복제본 여덟을 깔아 1픽셀 외곽선을 만든다.
    ///
    /// <paramref name="anchor"/> 는 원본과 **같은 것**을 줘야 한다. 예전에는 Center 로 박혀
    /// 있었는데, BottomRight 로 앉힌 글자에 쓰면 복제본만 화면 한가운데로 날아가 글이 두 벌
    /// 보인다. 「꾹 눌러서 넘기기」가 손님 얼굴 위에 겹쳐 뜬 것이 그 때문이었다.
    /// </summary>
    private static void AttachPixelOutline(TextMeshProUGUI source, Transform parent,
                                           Vector2 pos, TMP_FontAsset font, Vector2? anchor = null)
    {
        Vector2 use = anchor ?? Center;

        Vector2[] offsets =
        {
            new Vector2(-1f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, -1f), new Vector2(0f, 1f),
            new Vector2(-1f, -1f), new Vector2(1f, -1f),
            new Vector2(-1f, 1f), new Vector2(1f, 1f),
        };

        var copies = new TextMeshProUGUI[offsets.Length];

        for (int i = 0; i < offsets.Length; i++)
        {
            var copy = CreateTmpText("Outline" + i, parent, use, pos + offsets[i],
                                     source.rectTransform.sizeDelta, source.text,
                                     source.fontSize, font);
            copy.alignment = source.alignment;
            copy.raycastTarget = false;
            copy.color = Color.black;
            copies[i] = copy;
        }

        // 원본이 복사본들 위에 오도록 맨 뒤 형제로 보낸다.
        source.transform.SetAsLastSibling();

        var outline = Undo.AddComponent<PixelTextOutline>(source.gameObject);
        SetPrivateReference(outline, "source", source);
        SetPrivateArray(outline, "copies", copies);
    }

    /// <summary>배지 한 줄. 아이콘이 줄의 뿌리이고 글자가 그 자식이라 아이콘만 껐다 켜면 된다.</summary>
    private static SeasoningBadges.Row BuildSeasoningRow(Transform parent, TMP_FontAsset font,
                                                         string name, string iconFile, float y)
    {
        Image icon = CreateImage(name, parent, Center, new Vector2(BadgeX, y), BadgeIconSize,
                                 Color.white, LoadSprite(UiDir + iconFile));
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // 본문(12)으로는 아이콘 옆에서 눌려 보인다. 사이 값은 쓸 수 없어 제목 크기를 쓴다.
        var count = CreateTmpText("Count", icon.transform, Center, new Vector2(BadgeCountX, 0f),
                                  new Vector2(48f, 30f), "X 1", TextTitle, font);
        count.alignment = TextAlignmentOptions.Left;
        count.raycastTarget = false;
        count.color = Color.white;

        AttachPixelOutline(count, icon.transform, new Vector2(BadgeCountX, 0f), font);

        // 넣기 전에는 안 보인다. 보이는 채로 두면 0개와 안 넣은 것이 구별되지 않는다.
        icon.gameObject.SetActive(false);

        var row = new SeasoningBadges.Row();
        row.root = icon.gameObject;
        row.count = count;
        return row;
    }

    /// <summary>
    /// 먹는 연출이 도는 동안 감출 판을 모아 꽂아 준다.
    ///
    /// 이름으로 찾는다. 이것들은 서로 다른 함수에서 만들어져 한자리에 손잡이가 없고,
    /// 함수마다 반환값을 늘리는 것보다 여기서 한 번 훑는 편이 읽기 쉽다.
    /// </summary>
    private static void WireCinematicHiding(Transform canvas, OrderScreenRefs orderScreen)
    {
        if (orderScreen == null || orderScreen.Cutscene == null) return;

        var hidden = new System.Collections.Generic.List<Object>();

        // 조리 화면 판. 주문 화면 아래로 깔려 있어 바 사이로 비친다.
        Collect(hidden, canvas, "TopBar", "Slots", "NoodlePot", "Bowl", "DragLayer");

        // 주문 화면 자신의 상단 판.
        if (orderScreen.Root != null)
            Collect(hidden, orderScreen.Root.transform, "DayTimePanel", "RevenuePanel");

        SetPrivateArray(orderScreen.Cutscene, "hiddenDuringCut", hidden.ToArray());

        // 주문 화면이 떠 있는 동안 내려 둘 것. 그릇은 Canvas 가 얹혀 있어 계층 순서를 무시하고
        // 주문 화면 위로 떠오른다(BuildTutorialDim 아래의 LiftCanvas). 손님 얼굴을 덮는다.
        //
        // **조리 화면 상단바도 여기 들어간다.** 예전에는 정렬 0 이라 주문 화면(180)이 덮어 줬는데,
        // 비네트(182) 위로 올리면서 183 이 되어 더 이상 안 덮인다 — 주문 화면이 제 상단 패널
        // (DayTimePanel · RevenuePanel, 역시 183)을 띄우면 **둘이 겹쳐 찍힌다.**
        // 실제로 동전 아이콘이 두 개 나란히 뜨는 것으로 나타났다.
        var screenUI = Object.FindFirstObjectByType<OrderScreenUI>();
        if (screenUI != null)
        {
            var whileOpen = new System.Collections.Generic.List<Object>();
            Collect(whileOpen, canvas, "Bowl", "TopBar");
            SetPrivateArray(screenUI, "hiddenWhileOpen", whileOpen.ToArray());

            // 라멘을 낸 뒤에만 놓이는 그릇.
            if (orderScreen.Root != null)
            {
                // 그릇과 그림자를 함께 묶어 둔 자리. 그릇만 가리키면 그림자가 혼자 남는다.
                Transform served = orderScreen.Root.transform.Find("ServedBowl");
                if (served != null) SetPrivateReference(screenUI, "servedBowl", served.gameObject);

                // 시험용 — 손님 앞 그릇을 조리 화면 그릇의 복제본으로 바꾼다.
                // 조리 화면 그릇은 이 캔버스 직속이라 여기서야 손이 닿는다.
                var mirror = served != null ? served.GetComponent<ServedBowlMirror>() : null;
                Transform cooked = canvas.Find("Bowl");
                if (mirror != null && cooked != null)
                    SetPrivateReference(mirror, "source", (RectTransform)cooked);
            }
        }

        // 확대에 들어갈 때만 치우는 것. 첫 컷에는 손님 앞에 그릇이 놓여 있어야 한다.
        //
        // 카운터도 여기서 걷는다. 평소에는 손님 앞을 가려 "카운터 뒤에 서 있다"를 만드는
        // 판인데, 우주로 넘어간 뒤에도 남아 있으면 별밭 아래에 나무 바와 냄비가 그대로 보인다.
        // 손님 몸이 잘리는 높이는 자리에 씌운 마스크가 잡으므로 카운터가 없어도 그대로다.
        var atZoom = new System.Collections.Generic.List<Object>();
        if (orderScreen.Root != null) Collect(atZoom, orderScreen.Root.transform, "ServedBowl", "Counter");
        SetPrivateArray(orderScreen.Cutscene, "hiddenAtZoom", atZoom.ToArray());
    }

    private static void Collect(System.Collections.Generic.List<Object> into, Transform parent, params string[] names)
    {
        foreach (string name in names)
        {
            Transform found = parent.Find(name);
            if (found != null) into.Add(found.gameObject);
            else Debug.LogWarning("[RamenLayoutBuilder] 시네마틱에서 감출 " + name + " 을 찾지 못했습니다.");
        }
    }

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

    /// <summary>
    /// 되돌릴 수 없는 조작에 한 번 더 묻는 창. 폐기와 마무리가 같은 모양을 쓴다.
    /// 기획서 v1.2 3.2 — "폐기에는 확인창을 사용해 오작동을 막는다".
    ///
    /// 버튼은 둘이다. [넵]만 두면 잘못 연 사람이 빠져나갈 길이 없다.
    /// 여는 것과 Time.timeScale 처리는 ConfirmDialogUI가 한다.
    /// </summary>
    private static ConfirmDialogUI BuildConfirmDialog(Transform canvas, Bowl bowl,
                                                      string name, string message, bool isSubmit)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        // 바깥 껍데기는 늘 켜져 있어야 여는 쪽이 Open을 부를 수 있다. 껐다 켜는 것은 안쪽 Window다.
        Transform root = CreateGroup(name, canvas);
        Transform window = CreateGroup("Window", root);

        // 뒷판. raycastTarget을 켜 두어야 창이 떠 있는 동안 아래 조리 UI가 눌리지 않는다.
        var backdrop = CreateImage("Backdrop", window, Center, Vector2.zero, ScreenCover, new Color(0f, 0f, 0f, 0.6f));
        backdrop.raycastTarget = true;

        // 판과 버튼 모두 프로젝트의 TextBox.png 를 9-슬라이스로 쓴다. 조리 화면의 이름표와
        // 주문 화면의 제조하기 버튼이 쓰는 바로 그 그림이라, 새 모양을 만들지 않고 결이 맞는다.
        //
        // 폭 300 은 제목 한 줄이 안 접히는 최소선이다. "정말 폐기하시겠습니까?" 는 24픽셀
        // 글자 열 자에 띄어쓰기와 물음표가 붙어 264 쯤 된다. 253 이었을 때 "까?" 가 다음 줄로 넘어갔다.
        Image panel = CreateImage("Panel", window, Center, Vector2.zero, new Vector2(300f, 120f),
                                  Hex("#FFF8E7"), DialogBoxSprite());
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 1f;

        CreateTmpText("MessageText", panel.transform, Center, new Vector2(0f, 22f),
                      new Vector2(280f, 27f), message, TextTitle, tmpFont);

        // 버튼 둘을 가로로. 88 짜리 둘 사이를 24 띄운다.
        Button yes = MakeDialogButton("ConfirmButton", panel.transform, new Vector2(-56f, -26f),
                                      "넵", Hex("#C05A4A"), tmpFont);
        Button no = MakeDialogButton("CancelButton", panel.transform, new Vector2(56f, -26f),
                                     "아뇨", Hex("#7BA7C7"), tmpFont);

        // 튜토리얼 어두운 판(100) 위로 올린다. 그냥 두면 판이 확인창을 통째로 덮어
        // 창이 어둡게 깔리고, 무엇을 누르라는 것인지 안 보인다.
        LiftPopup(window.gameObject);

        // 마무리 확인창에서는 [넵] 이 빛난다. 폐기 창에는 붙이지 않는다 —
        // 폐기는 튜토리얼이 시키는 일이 아니라 잘못 넣었을 때 되돌리는 길이다.
        if (isSubmit) BuildDialogOutline(yes.image, TutorialOutline.Kind.Confirm);

        var ui = Undo.AddComponent<ConfirmDialogUI>(root.gameObject);
        SetPrivateReference(ui, "popupRoot", window.gameObject);
        SetPrivateReference(ui, "confirmButton", yes);
        SetPrivateReference(ui, "cancelButton", no);
        SetPrivateReference(ui, "bowl", bowl);
        SetPrivateField(ui, "isSubmit", isSubmit);

        window.gameObject.SetActive(false);

        return ui;
    }

    /// <summary>
    /// 팝업에 쓰는 상자 그림. 조리 화면 이름표와 주문 화면 제조하기 버튼이 쓰는 것과 같은 그림이다.
    /// 32x32 한 장을 테두리 8로 늘려 어떤 크기에도 모서리가 안 뭉개진다.
    /// </summary>
    private static Sprite DialogBoxSprite()
    {
        return LoadSlicedSprite(UiDir + "TextBox.png", new Vector4(8f, 8f, 8f, 8f));
    }

    /// <summary>
    /// 튜토리얼 안내판. 어두운 반투명이다(Tools/make_cooking_ui.py).
    ///
    /// 예전에는 크림색 불투명 판이었는데 **재료통 하나를 통째로 가렸다** — 「면을 그릇에
    /// 담아 주세요」라면서 면 통을 덮고 있으면 곤란하다. 어두운 반투명으로 내리면 밝은
    /// 나무 카운터 위에서 글자가 더 뜨면서 통 안도 계속 보인다.
    ///
    /// 32x32 타일을 테두리 10 으로 늘린다. <see cref="TutorialPrompt"/> 가 글 길이에 맞춰
    /// 판을 다시 잡으므로 통짜 그림을 쓰면 늘어나면서 둥근 모서리가 뭉개진다.
    /// </summary>
    private static Sprite TutorialPanelSprite()
    {
        return LoadSlicedSprite(GeneratedDir + "튜토리얼안내판.png", new Vector4(10f, 10f, 10f, 10f));
    }

    /// <summary>
    /// 마무리 버튼. 짙은 초록 옻칠에 금테다(Tools/make_cooking_ui.py).
    ///
    /// 예전에는 <c>Icon.png</c> 에서 잘라 쓰던 연두색 바였다. 화면에서 **유일하게 채도가
    /// 높은 색면**이라 양옆 두 판(날짜·수익)의 베벨 들어간 흰 판 사이에서 혼자 납작했다.
    /// 초록은 「제출」이라는 뜻을 이미 갖고 있어 버리지 않고 결만 맞췄다.
    ///
    /// 48x36 타일이다. **세로가 쓰는 크기와 같아서** 배율이 1이라 위아래 띠가 안 늘어난다.
    /// </summary>
    private static Sprite SubmitBarSprite()
    {
        return LoadSlicedSprite(GeneratedDir + "버튼_마무리.png", new Vector4(10f, 10f, 10f, 10f));
    }

    /// <summary>
    /// 팝업 버튼 하나. 제조하기 버튼과 같은 모양이라 크기와 슬라이스 설정을 같이 맞춰 둔다.
    /// </summary>
    private static Button MakeDialogButton(string name, Transform parent, Vector2 pos,
                                           string label, Color tint, TMP_FontAsset tmpFont)
    {
        Image image = CreateImage(name, parent, Center, pos, new Vector2(88f, 32f), tint, DialogBoxSprite());
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;

        var button = Undo.AddComponent<Button>(image.gameObject);
        button.targetGraphic = image;
        StyleButton(button);

        var text = CreateTmpText("Label", image.transform, Center, Vector2.zero,
                                 new Vector2(80f, 20f), label, TextBody, tmpFont);
        text.color = Color.white;

        return button;
    }

    /// <summary>
    /// 버튼 클릭을 확인창에 붙인다. 폐기와 마무리 둘 다 한 번 더 묻는다.
    /// 인스펙터에 남는 연결이라 씬을 저장하면 유지된다.
    /// </summary>
    private static void WireDialogButton(Image target, ConfirmDialogUI confirm)
    {
        var button = target.GetComponent<Button>();
        if (button == null) button = Undo.AddComponent<Button>(target.gameObject);

        button.targetGraphic = target;
        StyleButton(button);
        UnityEventTools.AddPersistentListener(button.onClick, confirm.Open);
    }

    /// <summary>확인창의 onConfirm 이벤트를 꺼낸다. private 필드라 리플렉션으로 잡는다.</summary>
    private static UnityEngine.Events.UnityEvent DialogConfirmEvent(ConfirmDialogUI dialog)
    {
        var field = typeof(ConfirmDialogUI).GetField("onConfirm",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var found = field != null ? field.GetValue(dialog) as UnityEngine.Events.UnityEvent : null;
        if (found != null) return found;

        var made = new UnityEngine.Events.UnityEvent();
        if (field != null) field.SetValue(dialog, made);
        return made;
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
                                typeof(ResultOrderNote),
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
            SetPrivateReference(resultUI, "targetProfitText", popup.TargetProfit);
            SetPrivateReference(resultUI, "profitText", popup.Profit);
            SetPrivateReference(resultUI, "totalProfitText", popup.TotalProfit);
            SetPrivateReference(resultUI, "averageAccuracyText", popup.Accuracy);
            SetPrivateReference(resultUI, "perfectCountText", popup.Perfect);
            SetPrivateReference(resultUI, "confirmButton", popup.Confirm);
            SetPrivateReference(resultUI, "retryButton", popup.Retry);
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
            SetPrivateReference(orderScreenUI, "dayClock", orderScreen.DayClock);
            SetPrivateReference(orderScreenUI, "revenueText", orderScreen.Revenue);
            SetPrivateReference(orderScreenUI, "dialogueText", orderScreen.Dialogue);
            SetPrivateReference(orderScreenUI, "dialogueViewport", orderScreen.DialogueViewport);
            SetPrivateReference(orderScreenUI, "startButton", orderScreen.Start);
            SetPrivateReference(orderScreenUI, "startButtonImage", orderScreen.StartImage);
            SetPrivateReference(orderScreenUI, "startButtonLabel", orderScreen.StartLabel);
            SetPrivateReference(orderScreenUI, "customerAppearance", orderScreen.Look);
            SetPrivateReference(orderScreenUI, "backdrop", orderScreen.Backdrop);
            if (orderScreen.Cutscene != null)
                SetPrivateReference(orderScreen.Cutscene, "orderScreen", orderScreenUI);

            // 글자가 찍힐 때마다 나는 톤. 사인파 한 토막을 코드로 만들어 pitch 만 바꿔 쓴다.
            SetPrivateReference(orderScreenUI, "blip", go.GetComponent<DialogueBlip>());
        }

        // 정보 패널(? 버튼). 이것도 root를 끄는 쪽이라 패널 바깥에 붙여야 한다.
        var book = go.GetComponent<RecipeBookUI>();
        if (recipeBook != null)
        {
            SetPrivateReference(book, "root", recipeBook.Root);
            SetPrivateArray(book, "menuNameTexts", recipeBook.MenuNameTexts);
            SetPrivateArray(book, "menuValueTexts", recipeBook.MenuValueTexts);
            SetPrivateReference(book, "closeButton", recipeBook.Close);

            // 미끄러지는 것은 판 하나다. root 는 껐다 켜기만 한다.
            SetPrivateReference(book, "panel", recipeBook.Panel);

            // 흐려지지 않는다. 알파를 1 로 굳혀 두는 데만 쓴다.
            SetPrivateReference(book, "fade", recipeBook.Fade);

            // 마우스를 올린 재료 이름.
            SetPrivateReference(book, "hoverName", recipeBook.HoverName);
            SetPrivateReference(book, "hoverLabel", recipeBook.HoverLabel);
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
            SetPrivateReference(result, "customerFace", orderResult.CustomerFace);
            SetPrivateReference(result, "confirmButton", orderResult.Confirm);
            SetPrivateReference(result, "servedBowl", orderResult.ServedBowl);

            // 결과창 옆에 붙박이로 서는 주문서. 종이는 Tab 주문서와 같은 것을 쓰지만
            // 미끄러지지도 흐려지지도 않아서 다루는 쪽이 따로 있다.
            var resultNote = go.GetComponent<ResultOrderNote>();
            SetPrivateReference(result, "note", resultNote);

            if (orderResult.Note != null)
            {
                SetPrivateReference(resultNote, "paper", orderResult.Note.Paper);
                SetPrivateReference(resultNote, "dialogueText", orderResult.Note.Dialogue);
                SetPrivateReference(resultNote, "dayLabel", orderResult.Note.DayLabel);
                SetPrivateReference(resultNote, "customerLabel", orderResult.Note.CustomerLabel);
                SetPrivateReference(resultNote, "dayManager", dayManager);
                SetPrivateReference(resultNote, "orderManager", manager);
                SetPrivateFloat(resultNote, "chromeHeight", NoteChromeHeight);
                SetPrivateFloat(resultNote, "textWidth", NoteTextWidth);
            }

            // 초상은 말투마다 다르다. 지금 손님이 누구인지는 손님을 세우는 쪽만 안다.
            if (orderScreen != null) SetPrivateReference(result, "customer", orderScreen.Look);
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
            SetPrivateReference(note, "dayLabel", orderNote.DayLabel);
            SetPrivateReference(note, "customerLabel", orderNote.CustomerLabel);
            SetPrivateReference(note, "dayManager", dayManager);
            SetPrivateReference(note, "fade", orderNote.Fade);
            SetPrivateFloat(note, "chromeHeight", NoteChromeHeight);
            SetPrivateFloat(note, "textWidth", NoteTextWidth);
            SetPrivateFloat(note, "hoverAlpha", NoteHoverAlpha);

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
    private static void WireGameManager(Transform canvas, OrderSystemRefs orderSystem, TopBarRefs topBar)
    {
        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        if (gameManager == null) return;

        SetPrivateReference(gameManager, "orderManager", orderSystem.Order);
        SetPrivateReference(gameManager, "dayManager", orderSystem.Day);
        SetPrivateReference(gameManager, "revenueText", topBar.RevenueText);
        SetPrivateReference(gameManager, "dayText", topBar.DayText);
        SetPrivateReference(gameManager, "dayClock", topBar.DayClock);
        SetPrivateReference(gameManager, "ramenCalculator", Object.FindFirstObjectByType<RamenCalculator>());
        SetPrivateReference(gameManager, "finalResultUI", Object.FindFirstObjectByType<FinalResultUI>());
        SetPrivateReference(gameManager, "orderScreenUI", Object.FindFirstObjectByType<OrderScreenUI>());
        SetPrivateReference(gameManager, "orderNoteUI", Object.FindFirstObjectByType<OrderNoteUI>());
        SetPrivateReference(gameManager, "titleScreen", Object.FindFirstObjectByType<TitleScreenUI>(FindObjectsInactive.Include));
        SetPrivateReference(gameManager, "orderResultUI", Object.FindFirstObjectByType<OrderResultUI>());
        SetPrivateReference(gameManager, "footsteps",
                            Object.FindFirstObjectByType<Footsteps>(FindObjectsInactive.Include));
        EatingCutscene cutsceneRef = Object.FindFirstObjectByType<EatingCutscene>(FindObjectsInactive.Include);
        SetPrivateReference(gameManager, "cutscene", cutsceneRef);

        // 「꾹 눌러서 넘기기」 게이지. 연출이 직접 켰다 끈다.
        if (cutsceneRef != null)
        {
            SetPrivateReference(cutsceneRef, "skipGauge",
                                Object.FindFirstObjectByType<HoldToSkip>(FindObjectsInactive.Include));
        }
        SetPrivateReference(gameManager, "narration",
                            Object.FindFirstObjectByType<OpeningNarration>(FindObjectsInactive.Include));
        SetPrivateReference(gameManager, "iris",
                            Object.FindFirstObjectByType<IrisFade>(FindObjectsInactive.Include));

        // 하루가 바뀔 때 뜨는 「N일차」.
        SetPrivateReference(gameManager, "dayTitle",
                            Object.FindFirstObjectByType<DayTitleUI>(FindObjectsInactive.Include));

        // 「튜토리얼을 보시겠습니까?」 물음판.
        SetPrivateReference(gameManager, "tutorialAsk",
                            Object.FindFirstObjectByType<TutorialAskUI>(FindObjectsInactive.Include));

        // 「주문마감」. 글자를 한 자씩 박는 박자는 ClosedSign 이 쥐고 있다.
        Transform closedSign = canvas.Find("ClosedSign");
        if (closedSign != null) SetPrivateReference(gameManager, "closedSign", closedSign.GetComponent<ClosedSign>());
        else Debug.LogWarning("[RamenLayoutBuilder] ClosedSign 을 찾지 못했습니다.");

        // 주문 확인은 Tab 을 누르고 있으면 된다(CookingHotkeys). 예전에는 상단바 왼쪽에
        // ? 버튼이 같은 것을 토글했는데, 그 자리를 Tab·B 키 안내에 내주면서 없앴다.
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

        AttachHoverOutline(button);
    }

    /// <summary>
    /// 마우스를 얹었을 때 뜨는 흰 테두리를 붙인다.
    ///
    /// Outline 효과는 같은 그림을 네 방향으로 밀어 뒤에 겹쳐 그린다. 버튼 실루엣을 그대로
    /// 따라가므로 버튼마다 테두리 그림을 구울 필요가 없다. 평소에는 ButtonHoverOutline 이 꺼 둔다.
    /// </summary>
    private static void AttachHoverOutline(Button button)
    {
        var outline = button.GetComponent<Outline>();
        if (outline == null) outline = Undo.AddComponent<Outline>(button.gameObject);

        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(2f, 2f);
        outline.useGraphicAlpha = false;
        outline.enabled = false;

        if (button.GetComponent<ButtonHoverOutline>() == null)
            Undo.AddComponent<ButtonHoverOutline>(button.gameObject);
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

    /// <summary>참조가 아니라 값(bool)인 private 필드를 채운다. 쓰임은 SetPrivateReference와 같다.</summary>
    private static void SetPrivateField(Object target, string fieldName, bool value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] " + target.GetType().Name + "." + fieldName + " 을(를) 찾지 못했습니다.");
            return;
        }

        property.boolValue = value;
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

    private static void SetPrivateFloat(Object target, string fieldName, float value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] " + target.GetType().Name + "." + fieldName + " 을(를) 찾지 못했습니다.");
            return;
        }

        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateInt(Object target, string fieldName, int value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            Debug.LogWarning("[RamenLayoutBuilder] " + target.GetType().Name + "." + fieldName + " 을(를) 찾지 못했습니다.");
            return;
        }

        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureGameManager()
    {
        // 이미 있으면 인스펙터 연결이 날아가지 않도록 그대로 둔다.
        if (Object.FindFirstObjectByType<GameManager>() == null)
        {
            var go = new GameObject("GameManager", typeof(GameManager));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        }

        // 튜토리얼도 같은 이유로 한 번만 만든다. 인스펙터의 "튜토리얼 켜기" 를 꺼 두고
        // 작업하는 경우가 많아서, 빌드할 때마다 다시 켜지면 곤란하다.
        if (Object.FindFirstObjectByType<TutorialManager>() == null)
        {
            var tutorial = new GameObject("TutorialManager", typeof(TutorialManager));
            Undo.RegisterCreatedObjectUndo(tutorial, UndoLabel);
        }
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
    /// <summary>
    /// 면 쏟기 시트 한 장. 6x7 로 잘리지만 그려진 것은 40칸이고 뒤 두 칸은 빈 칸이다.
    /// 빈 칸이 남아 있으면 마지막 장에서 소쿠리가 통째로 사라진다.
    /// </summary>
    private static Sprite[] NoodlePourSheet(string fileName)
    {
        Sprite[] frames = LoadSpriteSheet(CookDir + fileName, 128, 128);
        if (frames.Length > 40) System.Array.Resize(ref frames, 40);
        return frames;
    }

    /// <summary>
    /// 면통. 바구니 둘이 한 그림에 같이 그려져 있어서 슬롯 표로는 못 만든다.
    /// 그림 한 장을 깔고 그 위에 안 보이는 자리 둘을 얹어, 왼쪽은 얇은면 오른쪽은 굵은면을 맡긴다.
    ///
    /// 배치도대로 그릇 바로 아래 가운데다.
    /// </summary>
    private static void BuildNoodlePot(Transform canvas)
    {
        Sprite[] frames = LoadSpriteSheet(CookDir + "면통.png", 128, 128);

        // 그릇과 같은 256으로 놓는다. 128로 두면 그릇 옆에서 장난감처럼 작아 보인다.
        // 128칸 그림의 정확히 2배라 픽셀은 그대로다.
        // y 는 -140 이 한계다. 칸이 256 이라 그보다 내리면 아래변이 판(540) 밖으로 나간다.
        Image pot = CreateImage("NoodlePot", canvas, Center, new Vector2(0f, -140f),
                                new Vector2(256f, 256f), Color.white,
                                frames.Length > 0 ? frames[0] : null);
        pot.preserveAspect = true;
        pot.raycastTarget = false;   // 클릭은 위에 얹는 자리 둘이 받는다

        if (frames.Length > 1)
        {
            var loop = Undo.AddComponent<SpriteLoop>(pot.gameObject);
            loop.frames = frames;
        }

        // 안 보이는 자리 둘. 그림 안에서 바구니가 좌우로 나뉘어 있는 만큼만 잡는다.
        AddNoodleBasket(pot.transform, IngredientType.ThinNoodles, -60f);
        AddNoodleBasket(pot.transform, IngredientType.ThickNoodles, 60f);

        // 앞면에 다는 이름 팻말. 바구니(클릭 자리)보다 나중에 만들어 그 위에 그린다.
        AddNoodleSign(pot.transform, "얇은면", -NoodleSignX);
        AddNoodleSign(pot.transform, "굵은면", NoodleSignX);
    }

    // ── 면 팻말 ──────────────────────────────────────────────────
    //
    // 바구니 둘이 생김새가 거의 같아서, 눌러 보기 전에는 어느 쪽이 얇은면인지 알 수 없었다.
    // 튀김기 **앞면 은색 띠**에 작은 명패를 달고 이름을 쓴다.
    //
    // 자리는 그림(조리/면통.png 첫 칸)에서 재서 옮겨 적은 값이다. 통이 128칸 그림을 256으로
    // 놓은 것이라 칸 하나가 지역좌표 2다.
    //   앞면 띠   그림 y 84~98  ->  지역 y -40 ~ -68   (한가운데 -54)
    //   바구니    그림 x 43·85  ->  지역 x -41 · +43   (좌우 대칭으로 42 를 쓴다)
    // **통 그림을 바꾸면 이 값도 다시 재야 한다.**

    /// <summary>팻말이 바구니 한가운데에 맞춰 서는 자리.</summary>
    private const float NoodleSignX = 42f;

    /// <summary>앞면 띠(28칸)의 한가운데. 22칸짜리 판이 위아래로 3칸씩 남기고 앉는다.</summary>
    private const float NoodleSignY = -54f;

    /// <summary>판 크기. Tools/make_noodle_signs.py 가 구운 그대로 쓴다 — 늘리면 깎은 귀퉁이가 비뚤어진다.</summary>
    private static readonly Vector2 NoodleSignSize = new Vector2(68f, 22f);

    /// <summary>
    /// 면 바구니 앞에 다는 이름 팻말.
    ///
    /// 클릭을 받지 않는다. 팻말이 바구니 클릭 자리 위에 겹쳐 있어서, 받으면 팻말을 누른
    /// 사람만 면을 못 집는다. 눌러도 그대로 아래 바구니로 간다.
    /// </summary>
    private static void AddNoodleSign(Transform pot, string label, float x)
    {
        TMP_FontAsset tmpFont = EnsureTmpFont();

        Image plate = CreateImage("NoodleSign_" + label, pot, Center, new Vector2(x, NoodleSignY),
                                  NoodleSignSize, Color.white,
                                  LoadSprite(GeneratedDir + "면팻말.png"));
        plate.raycastTarget = false;

        var text = CreateTmpText("Label", plate.transform, Center, new Vector2(0f, 1f),
                                 new Vector2(NoodleSignSize.x - 8f, 16f), label, TextBody, tmpFont);
        text.color = DarkPanelInkColor;
        text.raycastTarget = false;
    }

    private static void AddNoodleBasket(Transform pot, IngredientType type, float x)
    {
        Image area = CreateImage("Basket_" + type, pot, Center, new Vector2(x, -12f),
                                 new Vector2(116f, 144f), new Color(0f, 0f, 0f, 0f));
        area.raycastTarget = true;

        var slot = Undo.AddComponent<NoodleSlot>(area.gameObject);
        slot.type = type;

        // 튜토리얼 안내 테두리. 튜토리얼이 시오 한 건이라 얇은면만 쓴다.
        //
        // 통 자체가 아니라 통 그림 위에 얹는다. 그림 한 장이 바구니 둘을 덮고 있어
        // 바구니 상자(투명한 클릭 자리)에는 뜰 실루엣이 없다. 통 그림과 같은 크기로
        // 겹쳐 두면 원본 칸 좌표가 그대로 맞아 자리를 따로 계산하지 않아도 된다.
        if (type != IngredientType.ThinNoodles) return;

        Sprite outline = ThinNoodleBasketOutline(TutorialOutlineThickness);
        if (outline == null) return;

        Image glow = CreateImage("TutorialOutline_" + type, pot, Center, Vector2.zero,
                                 (pot as RectTransform).sizeDelta, Color.white, outline);
        glow.preserveAspect = true;
        glow.raycastTarget = false;
        glow.enabled = false;

        var mark = Undo.AddComponent<TutorialOutline>(glow.gameObject);
        mark.type = type;
        mark.lift = LiftCanvas(pot.gameObject);
    }

    /// <summary>
    /// 얇은면 바구니 테두리. 통 그림 첫 칸에서 왼쪽 바구니가 앉은 자리에 타원 테를 그린다.
    ///
    /// 색으로 면을 골라 그 윤곽을 뜨는 방법을 먼저 썼는데 안 됐다. 면과 거품이 제각각이라
    /// 윤곽이 울퉁불퉁한 덩어리가 되고, 주황 손잡이까지 같이 잡혀 위로 삐져나왔다.
    /// 구멍을 메워도 면이 담긴 모양을 따라갈 뿐 바구니 테는 아니었다.
    ///
    /// 바구니는 어차피 고정된 그림이라 자리를 재서 박아 두는 편이 낫다.
    /// 아래 값은 통 그림 첫 칸을 줄 단위로 훑어 "면 또는 어두운 망" 이 이어지는
    /// 구간에서 잰 것이다. 가장 넓은 줄이 y 67~68 의 x 26~59 였고 세로는 y 53~79 였다.
    ///
    /// 통 그림을 바꾸면 이 값도 다시 재야 한다.
    /// </summary>
    private static Sprite ThinNoodleBasketOutline(int thickness)
    {
        const int Cell = 128;

        Texture2D texture = ReadableTexture(CookDir + "면통.png");
        if (texture == null) return null;

        Color[] src = texture.GetPixels(0, texture.height - Cell, Cell, Cell);
        var keep = new bool[Cell * Cell];

        // 1. 바구니. 한가운데에서 "면(주황) 또는 어두운 망" 을 따라 번져 나간 덩어리만 쓴다.
        //    통 여기저기의 어두운 선이 딸려 들어오지 않는다.
        var cand = new bool[Cell * Cell];
        for (int y = 0; y < Cell; y++)
        {
            for (int x = 0; x < Cell / 2; x++)
            {
                Color c = src[y * Cell + x];
                if (c.a <= 0.1f) continue;
                if (IsNoodleOrange(c) || c.r + c.g + c.b < 1.1f) cand[y * Cell + x] = true;
            }
        }

        var stack = new System.Collections.Generic.Stack<int>();
        int seed = BasketSeedY * Cell + BasketSeedX;
        if (cand[seed]) { stack.Push(seed); keep[seed] = true; }

        while (stack.Count > 0)
        {
            int i = stack.Pop();
            int cx = i % Cell;
            int cy = i / Cell;

            for (int k = 0; k < 4; k++)
            {
                int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0);
                int ny = cy + (k == 2 ? 1 : k == 3 ? -1 : 0);

                if (nx < 0 || ny < 0 || nx >= Cell / 2 || ny >= Cell) continue;

                int ni = ny * Cell + nx;
                if (!cand[ni] || keep[ni]) continue;

                keep[ni] = true;
                stack.Push(ni);
            }
        }

        // 2. 주황 손잡이. 바구니 위쪽에 떨어져 있다.
        for (int y = GripFromY; y < Cell; y++)
        {
            for (int x = 0; x < Cell / 2; x++)
            {
                Color c = src[y * Cell + x];
                if (c.a > 0.1f && IsNoodleOrange(c)) keep[y * Cell + x] = true;
            }
        }

        // 3. 둘을 잇는 쇠막대. 통 몸체와 색이 같아 색으로는 못 가른다. 두 끝을 띠로 잇는다.
        for (int t = 0; t <= 100; t++)
        {
            float k = t / 100f;
            int mx = Mathf.RoundToInt(Mathf.Lerp(34f, 27f, k));
            int my = Mathf.RoundToInt(Mathf.Lerp(74f, 88f, k));

            for (int dy = -RodRadius; dy <= RodRadius; dy++)
            {
                for (int dx = -RodRadius; dx <= RodRadius; dx++)
                {
                    if (dx * dx + dy * dy > RodRadius * RodRadius) continue;

                    int nx = mx + dx;
                    int ny = my + dy;
                    if (nx < 0 || ny < 0 || nx >= Cell || ny >= Cell) continue;

                    keep[ny * Cell + nx] = true;
                }
            }
        }

        // 4. 바구니 안쪽 구멍 메우기. 면과 거품 때문에 숭숭 뚫려 있어 그대로 뜨면 덩어리가 된다.
        //    바구니 높이에서 가로로만 메운다. 세로까지 메우면 비스듬한 막대 옆에 삼각형이 생긴다.
        for (int y = BasketFillFromY; y <= BasketFillToY; y++)
        {
            int first = -1;
            int last = -1;
            int count = 0;

            for (int x = 0; x < Cell / 2; x++)
            {
                if (!keep[y * Cell + x]) continue;
                if (first < 0) first = x;
                last = x;
                count++;
            }

            if (count <= 2) continue;
            for (int x = first; x <= last; x++) keep[y * Cell + x] = true;
        }

        // 5. 안쪽 테두리
        var made = new Color[Cell * Cell];
        var clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < Cell; y++)
        {
            for (int x = 0; x < Cell; x++)
            {
                int i = y * Cell + x;
                made[i] = clear;
                if (!keep[i]) continue;

                bool edge = false;
                for (int dy = -thickness; dy <= thickness && !edge; dy++)
                {
                    for (int dx = -thickness; dx <= thickness && !edge; dx++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx < 0 || ny < 0 || nx >= Cell || ny >= Cell) { edge = true; break; }
                        if (!keep[ny * Cell + nx]) edge = true;
                    }
                }

                if (edge) made[i] = Color.white;
            }
        }

        string path = SaveGenerated("Outline_ThinNoodles", EncodePng(made, Cell, Cell));
        return path == null ? null : LoadSprite(path);
    }

    /// <summary>면과 손잡이의 주황. 통은 회색 금속이라 세 값이 비슷해 걸리지 않는다.</summary>
    private static bool IsNoodleOrange(Color c)
    {
        return c.r > 0.45f && c.r > c.b + 0.15f && c.g > c.b;
    }

    /// <summary>
    /// 국자 붓기 시트 한 장. 6x6 으로 잘리지만 실제로 그려진 것은 35칸이고 마지막은 빈 칸이다.
    /// 빈 칸을 남겨 두면 CookingCursor 가 그것을 "빈 국자"로 집어 커서가 사라진다.
    /// </summary>
    private static Sprite[] LadleSheet(string fileName)
    {
        Sprite[] frames = LoadSpriteSheet(CookDir + fileName, 128, 128);
        if (frames.Length > 35) System.Array.Resize(ref frames, 35);
        return frames;
    }

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

        #pragma warning disable 0618 // spritesheet 폐기 예고 — LoadIconSprites 의 설명 참조
        // 이미 같은 개수로 잘려 있으면 다시 임포트하지 않는다. 재임포트는 느리다.
        // 칸 개수만 보면 안 된다. 그림 크기가 바뀌었는데 칸 수가 그대로면(손님 그림처럼
        // 높이만 달라진 경우) 옛 칸이 그대로 남아 스프라이트가 통째로 비어 버린다.
        bool sameCells = importer.spritesheet != null
                         && importer.spritesheet.Length == cols * rows
                         && importer.spritesheet.Length > 0
                         && Mathf.Approximately(importer.spritesheet[0].rect.width, cellWidth)
                         && Mathf.Approximately(importer.spritesheet[0].rect.height, cellHeight);

        bool needsSlice = importer.spriteImportMode != SpriteImportMode.Multiple || !sameCells;

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
        #pragma warning restore 0618

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
