using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// B 키로 여닫는 기본 레시피표. 왼쪽에서 미끄러져 나온다.
///
/// 기본 레시피만 담는다. 예전에는 재료 속성표(색·질감 같은 키워드)도 같이 실었는데,
/// 그게 사실상 주문 해석의 정답지라 퍼즐이 성립하지 않았다. 지금은 뺐다.
///
/// 손님 주문 내역은 여기 없다. 그쪽은 Tab으로 여는 OrderNoteUI가 맡는다.
/// 최종 정답은 보여 주지 않는다.
///
/// 기본 레시피는 B의 RecipeGenerator에서 그때그때 읽는다. 표를 이쪽에 베껴 두면
/// B가 값을 바꿨을 때 책과 채점이 어긋나기 때문이다.
/// </summary>
public class RecipeBookUI : MonoBehaviour
{
    // 아래는 RamenLayoutBuilder가 씬을 만들 때 꽂아 준다.
    [SerializeField] private GameObject root;
    /// <summary>메뉴 순서(<see cref="Menus"/>)대로 이름 글상자와 재료 글상자. 이름은 한 단계 큰 글자다.</summary>
    [SerializeField] private TextMeshProUGUI[] menuNameTexts;
    [SerializeField] private TextMeshProUGUI[] menuValueTexts;
    [SerializeField] private Button closeButton;

    /// <summary>아래에서 올라오는 판. 이것만 움직이고 root는 껐다 켜기만 한다.</summary>
    [SerializeField] private RectTransform panel;

    /// <summary>
    /// 판에 씌운 CanvasGroup. 빌더가 붙여 준다.
    ///
    /// 예전에는 마우스를 올리면 0.4 로 흐려져 뒤의 재료통이 비쳐 보였다. 지금은 <b>늘 진하다</b> —
    /// 책은 펼쳐 놓고 읽는 것이지 비쳐 보며 조리하는 것이 아니다. 대신 책이 덮은 자리는
    /// 클릭도 막는다(빌더에서 raycastTarget·blocksRaycasts 를 켠다). 안 보이는데 눌리면
    /// 책 뒤의 타래를 모르고 집게 된다.
    ///
    /// 여기서 알파를 다시 1 로 세우는 까닭은 씬에 옛 값(0.4)이 저장돼 있을 수 있어서다.
    /// </summary>
    [SerializeField] private CanvasGroup fade;

    /// <summary>재료 이름이 뜨는 글자. 흰 글자에 검은 외곽선(PixelTextOutline)이 둘려 있다.</summary>
    [SerializeField] private RectTransform hoverName;

    /// <summary>그 글자 본체. 이름을 여기에 쓴다.</summary>
    [SerializeField] private TextMeshProUGUI hoverLabel;

    /// <summary>다 나왔을 때 판 왼쪽에 남기는 여백.</summary>
    private const float ShownMargin = 8f;

    /// <summary>판의 세로 자리. 490 높이가 540 화면에 들어가도록 살짝 내려 앉힌다.</summary>
    private const float ShownY = -10f;

    /// <summary>
    /// 다 나왔을 때 판이 설 자리와 숨었을 때 자리. 판 폭에서 계산한다.
    ///
    /// 직렬화하지 않는다. [SerializeField] 로 두면 씬에 한 벌이 따로 저장되어, 여기를 고쳐도
    /// 화면은 옛 자리 그대로다. 실제로 아래에서 올라오던 시절 값이 씬에 남아 있었다.
    /// </summary>
    private Vector2 shownPosition;
    private Vector2 hiddenPosition;

    /// <summary>미끄러지는 데 걸리는 시간. 0.18 은 툭 튀어나오는 느낌이라 늦췄다.</summary>
    [SerializeField] private float slideSeconds = 0.4f;

    private Coroutine sliding;

    /// <summary>표에 싣는 순서. 조리 화면 오른쪽 재료통 순서와 맞춰 두면 눈이 덜 헤맨다.</summary>
    private static readonly IngredientType[] Toppings =
    {
        IngredientType.Menma, IngredientType.Chashu, IngredientType.GreenOnion,
        IngredientType.Nori, IngredientType.Egg, IngredientType.WoodEar,
        IngredientType.BeanSprout, IngredientType.FlavorOil, IngredientType.ChiliPowder
    };

    private static readonly Dictionary<IngredientType, string> KoreanNames = new Dictionary<IngredientType, string>
    {
        { IngredientType.Menma, "멘마" }, { IngredientType.Chashu, "차슈" },
        { IngredientType.GreenOnion, "파" }, { IngredientType.Nori, "김" },
        { IngredientType.Egg, "계란" }, { IngredientType.WoodEar, "목이버섯" },
        { IngredientType.BeanSprout, "숙주" }, { IngredientType.FlavorOil, "향미유" },
        { IngredientType.ChiliPowder, "고춧가루" }
    };

    /// <summary>수첩 그림에 그려진 재료 한 칸. 자리는 판(300x490) 기준, 가운데가 원점이다.</summary>
    private struct Cell
    {
        public readonly IngredientType Type;
        public readonly Vector2 Center;

        public Cell(IngredientType type, float x, float y)
        {
            Type = type;
            Center = new Vector2(x, y);
        }
    }

    // 재료 그림이 놓인 격자. **그림에서 재서 옮겨 적은 값이다** — 눈대중이 아니다.
    // 원본 레시피북.png(900x1470)에서 재료 덩어리의 가운데를 찾아 1/3 로 줄였다
    // (판이 300x490 이라 정확히 1/3 이다).
    //
    // 가로 넉 줄이 x -88 · -35 · 23 · 80 이고, 세로는 시오 두 줄 · 쇼유 두 줄 · 돈코츠 세 줄이다.
    // **그림을 다시 그리면 이 표도 같이 틀어진다.** 재료 자리를 옮겼으면 여기도 다시 잰다.
    private const float CellHalfX = 25f;   // 칸 사이가 53 이라 25 면 옆 칸을 안 문다
    private const float CellHalfY = 18f;   // 줄 사이가 39

    private static readonly Cell[] Cells =
    {
        // 시오 — 소금타래 · 육수 · 얇은면 · 차슈 / 멘마 둘 · 파 · 향미유
        new Cell(IngredientType.ShioTare,      -88f, 119f),
        new Cell(IngredientType.Broth,         -35f, 119f),
        new Cell(IngredientType.ThinNoodles,    23f, 119f),
        new Cell(IngredientType.Chashu,         80f, 119f),
        new Cell(IngredientType.Menma,         -88f,  80f),
        new Cell(IngredientType.Menma,         -35f,  80f),
        new Cell(IngredientType.GreenOnion,     23f,  80f),
        new Cell(IngredientType.FlavorOil,      80f,  80f),

        // 쇼유 — 간장타래 · 육수 · 얇은면 · 차슈 / 차슈 · 멘마 · 파 · 향미유
        new Cell(IngredientType.ShoyuTare,     -88f,   0f),
        new Cell(IngredientType.Broth,         -35f,   0f),
        new Cell(IngredientType.ThinNoodles,    23f,   0f),
        new Cell(IngredientType.Chashu,         80f,   0f),
        new Cell(IngredientType.Chashu,        -88f, -40f),
        new Cell(IngredientType.Menma,         -35f, -40f),
        new Cell(IngredientType.GreenOnion,     23f, -40f),
        new Cell(IngredientType.FlavorOil,      80f, -40f),

        // 돈코츠 — 베이스 · 육수 · 굵은면 · 차슈 / 계란 · 김 · 숙주 · 목이 / 파 · 향미유
        new Cell(IngredientType.TonkotsuBase,  -88f, -119f),
        new Cell(IngredientType.Broth,         -35f, -119f),
        new Cell(IngredientType.ThickNoodles,   23f, -119f),
        new Cell(IngredientType.Chashu,         80f, -119f),
        new Cell(IngredientType.Egg,           -88f, -159f),
        new Cell(IngredientType.Nori,          -35f, -159f),
        new Cell(IngredientType.BeanSprout,     23f, -159f),
        new Cell(IngredientType.WoodEar,        80f, -159f),
        new Cell(IngredientType.GreenOnion,    -88f, -198f),
        new Cell(IngredientType.FlavorOil,     -35f, -198f),
    };

    private static readonly RamenType[] Menus = { RamenType.Shio, RamenType.Shoyu, RamenType.Tonkotsu };

    /// <summary>빌더(RamenLayoutBuilder.BuildRecipeBook)도 읽는다 — 이름 글자 수로 밑줄 폭을 고른다.</summary>
    public static readonly string[] MenuNames = { "시오", "쇼유", "돈코츠" };

    public bool IsOpen
    {
        get { return root != null && root.activeSelf; }
    }

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        FillRecipeTable();
        LayoutPositions();

        // 이 스크립트는 root 바깥에 붙어 있어야 한다. 안에 있으면 자기 자신을 꺼 버린다.
        if (panel != null) panel.anchoredPosition = Snap(hiddenPosition);
        if (root != null) root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    /// <summary>
    /// 판이 숨는 자리와 나오는 자리를 판 폭에서 계산한다.
    ///
    /// 값을 적어 두면 판 폭을 바꿀 때마다 같이 고쳐야 한다. 주문서(OrderNoteUI)도 같은 방식이다.
    /// </summary>
    private void LayoutPositions()
    {
        if (panel == null) return;

        // 판이 놓인 그룹. 캔버스와 같은 크기고 가운데가 원점이다.
        var area = panel.parent as RectTransform;
        float half = area != null ? area.rect.width * 0.5f : 480f;
        float bookHalf = panel.sizeDelta.x * 0.5f;

        shownPosition = new Vector2(Mathf.Round(-half + ShownMargin + bookHalf), ShownY);

        // 숨을 때는 오른쪽 끝까지 화면 밖으로 나가야 한다. 조금 더 밀어 여유를 둔다.
        hiddenPosition = new Vector2(Mathf.Round(-half - bookHalf - 8f), ShownY);
    }

    /// <summary>B를 누르면 왼쪽에서 나온다.</summary>
    public void Show()
    {
        if (root != null) root.SetActive(true);

        // 책은 늘 진하다. 씬에 흐리던 시절 값(0.4)이 남아 있어도 여기서 되돌린다.
        if (fade != null) fade.alpha = 1f;

        // 지난번에 띄워 둔 이름이 남아 있으면 지운다.
        if (hoverName != null) hoverName.gameObject.SetActive(false);

        Slide(shownPosition, false);
    }

    /// <summary>
    /// 마우스가 어느 재료 그림 위에 있는지 보고 이름을 띄운다.
    ///
    /// 레이캐스트로 잡지 않는다. 재료는 낱개 오브젝트가 아니라 <b>수첩 그림 한 장에 그려져</b>
    /// 있어서 맞을 것이 없다. 대신 그림에서 재 둔 격자(<see cref="Cells"/>)에 마우스 자리를
    /// 견준다 — 보이지 않는 칸 스물여섯을 만들어 얹는 것보다 가볍고, 그림이 바뀌면 표 한 곳만 고친다.
    ///
    /// 캔버스가 Screen Space - Camera 라 그리는 카메라를 같이 넘긴다. null 을 넘기면
    /// 판이 화면 어디에 있는지 잘못 계산한다. (OrderNoteUI 와 같은 방식이다.)
    /// </summary>
    private void Update()
    {
        if (root == null || !root.activeSelf || panel == null || hoverName == null) return;

        if (Mouse.current == null)
        {
            ShowName(null, Vector2.zero);
            return;
        }

        Vector2 local;
        bool inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panel, Mouse.current.position.ReadValue(), CanvasPoint.CameraFor(panel), out local);

        if (!inside)
        {
            ShowName(null, Vector2.zero);
            return;
        }

        for (int i = 0; i < Cells.Length; i++)
        {
            Vector2 at = Cells[i].Center;
            if (Mathf.Abs(local.x - at.x) > CellHalfX) continue;
            if (Mathf.Abs(local.y - at.y) > CellHalfY) continue;

            ShowName(OrderManager.GetKoreanIngredientName(Cells[i].Type), at);
            return;
        }

        ShowName(null, Vector2.zero);
    }

    /// <summary>이름을 그 재료 위에 띄운다. <paramref name="text"/> 가 비면 감춘다.</summary>
    private void ShowName(string text, Vector2 at)
    {
        bool on = !string.IsNullOrEmpty(text);
        if (hoverName.gameObject.activeSelf != on) hoverName.gameObject.SetActive(on);
        if (!on) return;

        if (hoverLabel != null && hoverLabel.text != text) hoverLabel.text = text;

        // 반 칸에 놓이면 픽셀 글자가 흐려진다. 정수로 끊는다.
        hoverName.anchoredPosition = new Vector2(Mathf.Round(at.x), Mathf.Round(at.y));
    }

    /// <summary>다시 누르면 왼쪽으로 들어간다. 다 들어간 뒤에 끈다.</summary>
    public void Hide()
    {
        Slide(hiddenPosition, true);
    }

    public void Open()
    {
        Show();
        Sfx.Play("sfx_ui_book_open", 0.5f);
    }

    public void Close()
    {
        // 버튼으로 닫을 때도 미끄러져 내려간다.
        Hide();
        Sfx.Play("sfx_ui_book_close", 0.5f);
    }

    /// <summary>
    /// 판을 목표 자리로 미끄러뜨린다. 주문서(OrderNoteUI)와 같은 방식이다.
    ///
    /// 자리는 정수 칸으로 끊는다. 픽셀아트라 반 칸에 놓이면 테두리와 글자가 흐려진다.
    /// 시간은 실시간으로 잰다. 팝업이 떠서 게임이 멈춰 있어도 여닫혀야 한다.
    /// </summary>
    private void Slide(Vector2 target, bool disableWhenDone)
    {
        if (panel == null)
        {
            if (disableWhenDone && root != null) root.SetActive(false);
            return;
        }

        if (sliding != null) StopCoroutine(sliding);

        if (!gameObject.activeInHierarchy)
        {
            panel.anchoredPosition = Snap(target);
            if (disableWhenDone && root != null) root.SetActive(false);
            return;
        }

        sliding = StartCoroutine(SlideTo(target, disableWhenDone));
    }

    private IEnumerator SlideTo(Vector2 target, bool disableWhenDone)
    {
        Vector2 from = panel.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < slideSeconds)
        {
            float t = elapsed / slideSeconds;

            // 끝에서 부드럽게 멈춘다. 등속이면 툭 하고 서는 느낌이 난다.
            t = 1f - (1f - t) * (1f - t);

            panel.anchoredPosition = Snap(Vector2.Lerp(from, target, t));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        panel.anchoredPosition = Snap(target);
        sliding = null;

        if (disableWhenDone && root != null) root.SetActive(false);
    }

    private static Vector2 Snap(Vector2 v)
    {
        return new Vector2(Mathf.Round(v.x), Mathf.Round(v.y));
    }

    /// <summary>
    /// 기본 레시피는 RecipeGenerator에서 그대로 읽는다. 사본을 두지 않는다.
    ///
    /// 공책에 손으로 적은 모양이다. 메뉴마다 글상자가 둘이다(빌더가 자리와 크기를 잡는다).
    ///   돈코츠 :                              <- 이름 글상자, 한 단계 큰 글자, 밑에 빨간 물결 밑줄 그림
    ///   차슈 2 + 파 2 + 김 1 + 계란 1 + 목이버섯 1   <- 재료 글상자, 길면 "+" 뒤 빈칸에서 접힌다
    /// </summary>
    private void FillRecipeTable()
    {
        if (menuNameTexts == null || menuValueTexts == null) return;

        for (int i = 0; i < Menus.Length; i++)
        {
            if (i < menuNameTexts.Length && menuNameTexts[i] != null)
                menuNameTexts[i].text = MenuNames[i] + " :";

            if (i >= menuValueTexts.Length || menuValueTexts[i] == null) continue;

            Dictionary<IngredientType, int> recipe = RecipeGenerator.GetBaseRecipe(Menus[i]);
            var sb = new StringBuilder();

            bool first = true;
            foreach (IngredientType topping in Toppings)
            {
                int amount;
                if (!recipe.TryGetValue(topping, out amount) || amount <= 0) continue;

                if (!first) sb.Append(" + ");
                sb.Append(KoreanNames[topping]).Append(' ').Append(amount);
                first = false;
            }

            if (first) sb.Append("(토핑 없음)");
            menuValueTexts[i].text = sb.ToString();
        }
    }
}
