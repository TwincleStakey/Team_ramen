public class ChungcheongDialogueData : RegionDialogueData
{
    public override CustomerRegion Region => CustomerRegion.Chungcheong;

    public ChungcheongDialogueData()
    {
        muchWords = new[] { "넉넉하게", "듬뿍", "많이" };
        littleWords = new[] { "조금", "살짝" };
        addWords = new[] { "좀 더", "더" };

        SetTemplates(
            new[] { "{ramen} 주세유.", "{ramen}으로 해주세유." },
            new[] { "{ingredient} {amount}{unit} {add} 넣어주세유." },
            new[] { "{ingredient}은(는) 빼주세유.", "{ingredient}은(는) 옆사람 주면 되겠구만유." },
            new[] { "오늘은 {ramen}이 좋겠네유.", "{ramen} 같은 게 당기네유." },
            new[] { "{ingredient}이(가) {expression} 들어가면 좋겠네유." },
            new[] { "{ingredient}은(는) 알바생 지갑에 넣어주고." }
        );
    }
}
