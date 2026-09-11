using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// B 키를 누르고 있는 동안 아래에서 올라오는 기본 레시피표.
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

    /// <summary>다 올라왔을 때 판이 설 자리. 화면 아래쪽에 살짝만 띄운다 —
    /// 판 316 높이의 아래끝이 화면 아래(-270)에서 20칸 위에 온다.</summary>
    [SerializeField] private Vector2 shownPosition = new Vector2(0f, -92f);

    /// <summary>숨었을 때 자리. 화면 아래 바깥이라 판이 안 보인다.
    /// 판이 316 높이라 -400 이면 위쪽 코일이 28칸 비친다. 화면 반높이 270 + 판 반높이 158 보다 아래여야 한다.</summary>
    [SerializeField] private Vector2 hiddenPosition = new Vector2(0f, -440f);

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

        // 이 스크립트는 root 바깥에 붙어 있어야 한다. 안에 있으면 자기 자신을 꺼 버린다.
        if (panel != null) panel.anchoredPosition = Snap(hiddenPosition);
        if (root != null) root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    /// <summary>B를 누르고 있는 동안 아래에서 올라온다.</summary>
    public void Show()
    {
        if (root != null) root.SetActive(true);
        Slide(shownPosition, false);
    }

    /// <summary>떼면 다시 아래로 내려간다. 다 내려간 뒤에 끈다.</summary>
    public void Hide()
    {
        Slide(hiddenPosition, true);
    }

    public void Open()
    {
        Show();
    }

    public void Close()
    {
        // 버튼으로 닫을 때도 미끄러져 내려간다.
        Hide();
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
