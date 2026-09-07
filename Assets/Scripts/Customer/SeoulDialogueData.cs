public class SeoulDialogueData : RegionDialogueData
{
    public override CustomerRegion Region => CustomerRegion.Seoul;

    public SeoulDialogueData()
    {
        muchWords = new[] { "많이", "듬뿍", "푸짐하게" };
        littleWords = new[] { "조금", "살짝" };
        addWords = new[] { "더", "좀 더" };

        SetTemplates(
            new[] {"{ramen} 주세요.", "{ramen}으로 부탁드릴게요."},
            new[] {"{ingredient} {add} 넣어주세요."},
            new[] {"{ingredient}는 빼주세요.", "{ingredient}는 넣지 말아주세요."},
            new[] {"오늘은 {ramen}이 당기네요.", "{ramen} 같은 게 좋겠어요."},
            new[] {"{ingredient}이(가) {expression} 들어가면 좋겠어요."},
            new[] {"{ingredient}는 안 먹을 것 같아요.", "{ingredient}은 괜찮을 것 같아요."}
        );
    }
}
