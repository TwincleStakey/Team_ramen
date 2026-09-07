using System.Collections.Generic;

public static class RegionDialogueDatabase
{
    private static readonly Dictionary<CustomerRegion, RegionDialogueData>
        regionData = new Dictionary<CustomerRegion, RegionDialogueData>
        {
            { CustomerRegion.Seoul, new SeoulDialogueData() },
            { CustomerRegion.Chungcheong, new ChungcheongDialogueData() },
            { CustomerRegion.Jeolla, new JeollaDialogueData() },
            { CustomerRegion.Gyeongsang, new GyeongsangDialogueData() }
        };

    public static RegionDialogueData Get(CustomerRegion region)
    {
        return regionData[region];
    }
}
