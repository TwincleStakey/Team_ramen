using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// B 키로 여닫는 레시피 책. 기본 레시피와 재료 속성표를 담는다.
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
    [SerializeField] private TextMeshProUGUI recipeNames;
    [SerializeField] private TextMeshProUGUI recipeValues;
    [SerializeField] private TextMeshProUGUI ingredientNames;
    [SerializeField] private TextMeshProUGUI ingredientAttrs;
    [SerializeField] private Button closeButton;

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

    /// <summary>기획서 4.2 재료 속성표 + 19.4 묘사. 퍼즐의 사전이라 문구를 임의로 바꾸면 안 된다.</summary>
    private static readonly string[] Attributes =
    {
        "채소 고명   ·  갈색, 길쭉함  ·  아삭함",
        "고기 고명   ·  갈색, 둥근    ·  고기, 묵직함",
        "채소 고명   ·  초록색        ·  향이 강함",
        "고명        ·  검은색        ·  바다 향",
        "고명        ·  흰색, 노란색  ·  부드러움",
        "채소 고명   ·  갈색          ·  쫄깃함",
        "채소 고명   ·  흰색, 가늘음  ·  아삭함",
        "조미료      ·  기름          ·  향, 기름짐",
        "조미료      ·  빨간색        ·  매움"
    };

    private static readonly RamenType[] Menus = { RamenType.Shio, RamenType.Shoyu, RamenType.Tonkotsu };
    private static readonly string[] MenuNames = { "시오", "쇼유", "돈코츠" };

    public bool IsOpen
    {
        get { return root != null && root.activeSelf; }
    }

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        FillIngredientTable();
        FillRecipeTable();

        // 이 스크립트는 root 바깥에 붙어 있어야 한다. 안에 있으면 자기 자신을 꺼 버린다.
        Close();
    }

    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    public void Open()
    {
        if (root != null) root.SetActive(true);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
    }

    private void FillIngredientTable()
    {
        if (ingredientNames == null || ingredientAttrs == null) return;

        var names = new StringBuilder();
        var attrs = new StringBuilder();

        for (int i = 0; i < Toppings.Length; i++)
        {
            if (i > 0) { names.Append('\n'); attrs.Append('\n'); }
            names.Append(KoreanNames[Toppings[i]]);
            attrs.Append(Attributes[i]);
        }

        ingredientNames.text = names.ToString();
        ingredientAttrs.text = attrs.ToString();
    }

    /// <summary>
    /// 기본 레시피는 B의 RecipeGenerator에서 읽는다.
    /// 변경 요청이 비어 있는 주문을 넘기면 기본 레시피가 그대로 돌아온다.
    /// </summary>
    private void FillRecipeTable()
    {
        if (recipeNames == null || recipeValues == null) return;

        var generator = new RecipeGenerator();
        var names = new StringBuilder();
        var values = new StringBuilder();

        for (int i = 0; i < Menus.Length; i++)
        {
            if (i > 0) { names.Append('\n'); values.Append('\n'); }
            names.Append(MenuNames[i]);

            Dictionary<IngredientType, int> recipe =
                generator.GenerateTargetRecipe(new CustomerOrder { ramenType = Menus[i] });

            bool first = true;
            foreach (IngredientType topping in Toppings)
            {
                int amount;
                if (!recipe.TryGetValue(topping, out amount) || amount <= 0) continue;

                if (!first) values.Append("   ");
                values.Append(KoreanNames[topping]).Append(' ').Append(amount);
                first = false;
            }

            if (first) values.Append("(토핑 없음)");
        }

        recipeNames.text = names.ToString();
        recipeValues.text = values.ToString();
    }
}
