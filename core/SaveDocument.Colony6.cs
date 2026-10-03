namespace XbdeEditor.Core;

public sealed record Colony6State(int Development, int Population, int Housing, int Commerce,
    int Nature, int Special, int OverallLevel, bool CanMaximize, string? UnavailableReason)
{
    public bool IsMaximum => Housing == 5 && Commerce == 5 && Nature == 5 && Special == 5 && OverallLevel == 5;
}

public sealed partial class SaveDocument
{
    private const int Colony6Offset = 0xcf8;
    private sealed record Colony6Upgrade(int Development, int Population, int EffectFlag, int SelfFlag, int QuestFlag);

    // CL6_uplist rows 1–25, in the order consumed by native upgrade function 0x27c170.
    private static readonly Colony6Upgrade[] Colony6Upgrades =
    [
        new(2, 4, 0, 51, 991), new(2, 8, 0, 52, 992), new(3, 15, 0, 53, 993), new(3, 18, 0, 54, 994), new(3, 27, 46, 55, 995),
        new(2, 1, 0, 0, 0), new(2, 1, 0, 96, 0), new(2, 1, 0, 0, 0), new(3, 1, 0, 0, 0), new(4, 2, 0, 0, 982),
        new(1, 0, 0, 0, 984), new(2, 0, 38, 0, 985), new(2, 0, 0, 0, 986), new(3, 0, 0, 0, 987), new(5, 3, 0, 0, 998),
        new(3, 0, 0, 0, 934), new(3, 2, 39, 0, 990), new(4, 0, 53, 0, 997), new(5, 0, 44, 0, 937), new(6, 3, 29, 0, 938),
        new(0, 0, 32, 0, 0), new(0, 0, 33, 0, 0), new(0, 0, 34, 0, 0), new(0, 0, 35, 0, 0), new(0, 0, 36, 0, 0)
    ];

    public Colony6State? Colony6
    {
        get
        {
            if (Campaign != Campaign.MainStory) return null;
            string? reason = Colony6Restriction();
            return new(ReadByte(Colony6Offset), ReadByte(Colony6Offset + 1),
                ReadByte(Colony6Offset + 2), ReadByte(Colony6Offset + 3), ReadByte(Colony6Offset + 4),
                ReadByte(Colony6Offset + 5), ReadByte(Colony6Offset + 6), reason is null, reason);
        }
    }

    public void MaximizeColony6()
    {
        if (Colony6Restriction() is { } reason) throw new ArgumentException(reason);
        var upgrades = MissingColony6Upgrades();
        if (upgrades.Count == 0) return;
        foreach (var upgrade in upgrades)
        {
            if (upgrade.EffectFlag != 0)
            {
                int bit = 0x1d6a + upgrade.EffectFlag;
                int offset = 0x50 + bit / 8;
                WriteByte(offset, (byte)(ReadByte(offset) | (1 << (bit % 8))));
            }
            if (upgrade.SelfFlag != 0) WriteByte(0xc94 + upgrade.SelfFlag, 1);
            if (upgrade.QuestFlag != 0) WriteByte(0x5f0 + upgrade.QuestFlag, 200);
        }
        WriteByte(Colony6Offset, (byte)(ReadByte(Colony6Offset) + upgrades.Sum(step => step.Development)));
        WriteByte(Colony6Offset + 1, (byte)(ReadByte(Colony6Offset + 1) + upgrades.Sum(step => step.Population)));
        for (int category = 2; category <= 6; category++) WriteByte(Colony6Offset + category, 5);
    }

    private string? Colony6Restriction()
    {
        if (Campaign != Campaign.MainStory || ReadUInt32(0) != 7)
            return "Colony 6 reconstruction requires an identified main-story save with format version 7.";
        int[] levels = Enumerable.Range(2, 5).Select(index => (int)ReadByte(Colony6Offset + index)).ToArray();
        if (levels.Any(level => level > 5) || levels[4] > levels.Take(4).Min())
            return "Colony 6 levels are inconsistent; reconstruction was not changed.";
        if (levels.Take(4).All(level => level == 0))
            return "Complete the first Colony 6 reconstruction upgrade in-game before using this operation.";
        var upgrades = MissingColony6Upgrades();
        if (ReadByte(Colony6Offset) + upgrades.Sum(step => step.Development) > byte.MaxValue
            || ReadByte(Colony6Offset + 1) + upgrades.Sum(step => step.Population) > byte.MaxValue)
            return "Colony 6 development or population would overflow its saved byte.";
        if (upgrades.Any(step => step.SelfFlag != 0 && ReadByte(0xc94 + step.SelfFlag) > 1
            || step.QuestFlag != 0 && ReadByte(0x5f0 + step.QuestFlag) > 200))
            return "A linked Colony 6 flag has an unrecognized state; reconstruction was not changed.";
        return null;
    }

    private IReadOnlyList<Colony6Upgrade> MissingColony6Upgrades()
    {
        var steps = new List<Colony6Upgrade>();
        for (int category = 2; category <= 6; category++)
            for (int level = ReadByte(Colony6Offset + category); level < 5; level++)
                steps.Add(Colony6Upgrades[(category - 2) * 5 + level]);
        return steps;
    }
}
