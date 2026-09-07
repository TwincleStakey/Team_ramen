public class GyeongsangDialogueData : RegionDialogueData
{
    public override CustomerRegion Region => CustomerRegion.Gyeongsang;

    public GyeongsangDialogueData()
    {
        muchWords = new[] { "마이", "억수로", "푸짐하게" };
        littleWords = new[] { "쪼매", "조금" };
        addWords = new[] { "더", "좀 더" };

        SetTemplates(
            new[] { "{ramen} 주이소.", "{ramen}으로 없습니까?" },  // 직설형
            new[] { "{ingredient} {add} 넣어주이소." },
            new[] { "{ingredient}는 빼주이소.", "{ingredient}는 안 넣어주이소." },
            new[] { "오늘은 {ramen}이 땡기네예.", "{ramen} 같은 게 좋겠네예." },  // 간접형
            new[] { "{ingredient} {expression} 단디 챙겨 주시소." },
            new[] { "{ingredient}는 없는 게 좋겠네예." }
        );
    }
}
