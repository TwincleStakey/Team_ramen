using System.Collections.Generic;
using UnityEngine;

public static class OrderExpressionDatabase
{
    private static readonly Dictionary<RamenType, HintData> ramenHints = new Dictionary<RamenType, HintData>
        {
            { RamenType.Shio, new HintData(
                new[] { "시오 라멘", "소금 라멘" },
                new[] { "담백한 국물의 라멘", "깔끔한 맛의 라멘", "소금으로 간한 라멘" }) },
            { RamenType.Shoyu, new HintData(
                new[] { "쇼유 라멘", "간장 라멘" },
                new[] { "간장 향이 나는 라멘", "짭조름한 국물의 라멘", "간장 맛이 은은한 라멘" }) },
            { RamenType.Tonkotsu, new HintData(
                new[] { "돈코츠 라멘", "돼지뼈 육수 라멘" },
                new[] { "뽀얗고 진한 국물의 라멘", "고소한 국물의 라멘", "진하게 우려낸 국물의 라멘" }) }
        };

    private static readonly Dictionary<IngredientType, HintData> ingredientHints = new Dictionary<IngredientType, HintData>
        {
            { IngredientType.Chashu, new HintData(
                new[] { "차슈", "고기" }, new[] { "돼지고기 토핑", "푸짐한 고기", "고기 토핑" }) },
            { IngredientType.Menma, new HintData(
                new[] { "멘마", "죽순" }, new[] { "아삭한 죽순 토핑", "씹는 맛이 있는 토핑" }) },
            { IngredientType.GreenOnion, new HintData(
                new[] { "파", "대파" }, new[] { "초록색 향채", "파 향 나는 것", "향긋한 채소" }) },
            { IngredientType.Egg, new HintData(
                new[] { "계란", "달걀" }, new[] { "노른자 있는 토핑", "부드러운 달걀", "계란 토핑" }) },
            { IngredientType.Nori, new HintData(
                new[] { "김", "김 토핑" }, new[] { "바다 향 나는 것", "검은 해조류", "김 같은 토핑" }) },
            { IngredientType.BeanSprout, new HintData(
                new[] { "숙주", "숙주나물" }, new[] { "아삭한 흰 채소", "아삭한 나물" }) },
            { IngredientType.WoodEar, new HintData(
                new[] { "목이버섯", "버섯" }, new[] { "검은 버섯 토핑", "꼬들꼬들한 버섯" }) },
            { IngredientType.FlavorOil, new HintData(
                new[] { "향미유", "향기름" }, new[] { "향을 내는 기름", "고소한 향을 더하는 것" }) },
            { IngredientType.ChiliPowder, new HintData(
                new[] { "고춧가루", "매운 가루" }, new[] { "칼칼한 맛", "붉은 매운 양념" }) }
        };

    private static readonly Dictionary<IngredientType, string> ingredientUnits = new Dictionary<IngredientType, string>
        {
            { IngredientType.Chashu, "장" }, { IngredientType.Menma, "개" },
            { IngredientType.GreenOnion, "번" }, { IngredientType.Egg, "개" },
            { IngredientType.Nori, "장" }, { IngredientType.BeanSprout, "번" },
            { IngredientType.WoodEar, "번" }, { IngredientType.FlavorOil, "번" },
            { IngredientType.ChiliPowder, "번" }
        };

    public static string GetRamenExpression(RamenType ramenType, OrderExpressionStyle style)
    {
        return GetHint(ramenHints[ramenType], style);
    }

    public static string GetIngredientExpression(IngredientType ingredient, OrderExpressionStyle style)
    {
        return GetHint(ingredientHints[ingredient], style);
    }

    public static string GetIngredientUnit(IngredientType ingredient)
    {
        return ingredientUnits[ingredient];
    }

    private static string GetHint(HintData data, OrderExpressionStyle style)
    {
        string[] values = style == OrderExpressionStyle.Direct ? data.direct : data.indirect;

        return values[Random.Range(0, values.Length)];
    }
}