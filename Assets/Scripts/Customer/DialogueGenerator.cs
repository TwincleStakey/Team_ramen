using System.Text;

public static class DialogueGenerator
{
    /// <summary>
    /// 손님 주문 데이터를 대사로 변환한다.
    /// </summary>
    public static string GenerateDialogue(CustomerOrder order)
    {
        StringBuilder dialogue =new StringBuilder();

        // 기본 라멘
        dialogue.Append(GetRamenName(order.ramenType));

        dialogue.Append(" 하나 주세요.");

        // 추가 주문

        foreach (IngredientRequest request in order.requests)
        {
            dialogue.Append("\n");

            dialogue.Append(GetIngredientDialogue(request.ingredient, request.amount));
        }

        return dialogue.ToString();
    }

    // 라멘 이름

    private static string GetRamenName(RamenType ramenType)
    {
        switch (ramenType)
        {
            case RamenType.Shio:
                return "시오 라멘";

            case RamenType.Shoyu:
                return "쇼유 라멘";

            case RamenType.Tonkotsu:
                return "돈코츠 라멘";
        }

        return "라멘";
    }

    // 재료별 대사

    private static string GetIngredientDialogue(IngredientType ingredient, int amount)
    {
        switch (ingredient)
        {
            case IngredientType.Chashu:

                switch (amount)
                {
                    case 1:
                        return "차슈 조금 더 넣어주세요.";

                    case 2:
                        return "차슈 많이 넣어주세요.";

                    case 3:
                        return "고기 향을 많이 느끼고 싶어요";
                }

                break;

            case IngredientType.FlavorOil:

                switch (amount)
                {
                    case 1:
                        return "향미유 조금 더 넣어주세요.";

                    case 2:
                        return "향미유 좀 더 넣어주세요.";

                    case 3:
                        return "향미유 많이 넣어주세요.";
                }

                break;

            case IngredientType.ChiliPowder:

                switch (amount)
                {
                    case 1:
                        return "조금 맵게 해주세요.";

                    case 2:
                        return "맵게 해주세요.";

                    case 3:
                        return "칼칼하게 해주세요.";
                }

                break;

            case IngredientType.Menma:

                switch (amount)
                {
                    case 1:
                        return "죽순 있으면 하나만 넣어주세요.";

                    case 2:
                        return "멘마 많이 넣어주세요.";

                    case 3:
                        return "멘마 많이많이 넣어주세요.";
                }

                break;

            case IngredientType.GreenOnion:

                switch (amount)
                {
                    case 1:
                        return "파 살짝만 더 넣어주세요.";

                    case 2:
                        return "파 많이 넣어주세요.";
                }

                break;

            case IngredientType.Egg:

                switch (amount)
                {
                    case 1:
                        return "계란 하나 더 풀어주세요.";

                    case 2:
                        return "계란 많이 넣어주세요.";

                    case 3:
                        return "계란 많이많이 넣어주세요.";
                }

                break;

            case IngredientType.BeanSprout:

                switch (amount)
                {
                    case 1:
                        return "숙주 조금 더 넣어주세요.";

                    case 2:
                        return "숙주 많이 넣어주세요.";

                    case 3:
                        return "숙주 많이많이 넣어주세요.";
                }

                break;

            case IngredientType.WoodEar:

                switch (amount)
                {
                    case 1:
                        return "목이버섯 조금 더 넣어주세요.";

                    case 2:
                        return "목이버섯 많이 넣어주세요.";

                    case 3:
                        return "목이버섯 많이많이 넣어주세요.";
                }

                break; 

            case IngredientType.Nori:

                switch (amount)
                {
                    case 1:
                        return "김 하나만 더 넣어주세요.";

                    case 2:
                        return "김 많이 넣어주세요.";

                    case 3:
                        return "바다 향기 많이 느끼고 싶어요";
                }

                break;
        }


        return "";
    }
}