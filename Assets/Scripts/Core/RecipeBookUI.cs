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

    /// <summary>판 전체를 한꺼번에 흐리게 하는 데 쓴다. 빌더가 판에 붙여 준다.</summary>
    [SerializeField] private CanvasGroup fade;

    /// <summary>
    /// 마우스가 판 위에 있을 때의 진하기. 뒤에 있는 재료가 비쳐 보이는 정도.
    ///
    /// 책은 이제 토글이라 켜 둔 채로 조리할 수 있는데, 왼쪽 타래 3통과 육수 냄비를 통째로 덮는다.
    /// 주문서와 같은 방식으로 비켜 준다. 값도 주문서에 맞춘다 — 둘이 다르면 손이 헷갈린다.
    /// </summary>
    [SerializeField, Range(0.1f, 1f)] private float hoverAlpha = 0.4f;

    /// <summary>흐려지고 돌아오는 데 걸리는 시간.</summary>
    [SerializeField] private float fadeSeconds = 0.1f;

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

        // 흐려진 채로 닫혔다가 다시 나오면 흐린 상태로 시작한다. 나올 때는 늘 진하게.
        if (fade != null) fade.alpha = 1f;

        Slide(shownPosition, false);
    }

    /// <summary>
    /// 마우스가 판 위에 오면 흐려진다. 책이 왼쪽 타래·육수를 덮고 있어도 뒤가 비쳐 보여야 한다.
    ///
    /// 레이캐스트를 쓰지 않는다. 판에 raycastTarget 을 켜는 순간 판이 클릭을 가로채
    /// 뒤에 있는 재료통을 못 만지게 된다. 그래서 상자 안에 들었는지만 좌표로 본다.
    /// 캔버스가 Screen Space - Camera 라 그리는 카메라를 같이 넘긴다 — null 을 넘기면
    /// 판이 화면 어디에 있는지 잘못 계산한다. (OrderNoteUI 와 같은 방식이다.)
    /// </summary>
    private void Update()
    {
        if (fade == null || root == null || !root.activeSelf) return;

        bool over = panel != null && Mouse.current != null
                    && RectTransformUtility.RectangleContainsScreenPoint(
                           panel, Mouse.current.position.ReadValue(), CanvasPoint.CameraFor(panel));

        float target = over ? hoverAlpha : 1f;

        // 팝업이 떠서 게임이 멈춰 있어도 책은 반응해야 한다.
        fade.alpha = fadeSeconds <= 0f
            ? target
            : Mathf.MoveTowards(fade.alpha, target, Time.unscaledDeltaTime / fadeSeconds);
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
