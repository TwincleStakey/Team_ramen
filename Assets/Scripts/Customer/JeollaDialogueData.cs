public class JeollaDialogueData : RegionDialogueData
{
    public override CustomerRegion Region => CustomerRegion.Jeolla;

    public JeollaDialogueData()
    {
        muchWords = new[] { "겁나게", "허벌나게", "푸짐하게" };
        littleWords = new[] { "쪼끔", "조금" };
        addWords = new[] { "좀 더", "더" };

        SetTemplates(
            new[] { "사장님, {ramen} 주쇼잉.", "{ramen}으로 해주쇼." },
            new[] { "{ingredient} {amount}{unit} {add} 넣어주쇼." },
            new[] { "{ingredient}는 빼주쇼.", "뭐단다고 {ingredient}을(를) 넣어버리냐." },
            new[] { "오늘은 {ramen}이 좋겄네요잉.", "{ramen} 같은 게 당기네요잉." },
            new[] { "{ingredient}이(가) {expression} 들어가면 좋겄네요잉." },
            new[] { "{ingredient}는 괜찮아요잉" }
        );
    }
}
