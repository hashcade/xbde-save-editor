namespace XbdeEditor.Core;

public static class LevelProgression
{
    public const uint MaximumLevel = 99;
    public const uint MaximumExperience = 99_999_999;

    // BTL_growlist.level_exp: the cost to reach each level, not cumulative EXP.
    private static readonly uint[] Costs =
    [
        0, 0, 40, 60, 145, 208, 280, 338, 400, 436, 507, 585, 707, 790, 928,
        1016, 1170, 1263, 1432, 1531, 1716, 1924, 2238, 2457, 2802, 3031,
        3408, 3647, 4056, 4305, 4745, 5265, 6016, 6557, 7371, 7932, 8808,
        9391, 10329, 10933, 11934, 13182, 14911, 16200, 18054, 19385, 21364,
        22737, 24840, 26254, 28483, 31395, 35287, 38282, 42424, 45502, 49894,
        53055, 57696, 60941, 65832, 72488, 81122, 87945, 97078, 104067,
        113700, 120855, 130988, 138309, 148941, 163917, 182868, 198177,
        218127, 233768, 254716, 270691, 292637, 308945, 331890, 365170,
        406419, 440364, 483610, 518221, 563464, 598741, 645980, 681922,
        731159, 804375, 893547, 968094, 1061260, 1137138, 1234298, 1311507,
        1412660, 1491201
    ];

    public static uint MinimumLevel(int characterId, Campaign campaign) => campaign switch
    {
        Campaign.MainStory => characterId switch
        {
            1 or 2 => 1, 3 => 2, 4 => 20, 5 => 10, 6 => 22, 7 => 23, 8 => 40,
            _ => throw new ArgumentException("Progression editing is not supported for this character.")
        },
        Campaign.FutureConnected => characterId switch
        {
            1 or 7 => 60, 14 or 15 => 58,
            _ => throw new ArgumentException("Progression editing is not supported for this character.")
        },
        _ => throw new ArgumentException("Progression editing requires an identified campaign.")
    };

    public static uint ExperienceToNextLevel(uint level)
    {
        if (level is < 1 or > MaximumLevel) throw new ArgumentOutOfRangeException(nameof(level));
        return level == MaximumLevel ? 0 : Costs[level + 1];
    }

    public static (uint Level, uint Experience) Normalize(uint level, uint experience)
    {
        if (level is < 1 or > MaximumLevel) throw new ArgumentOutOfRangeException(nameof(level));
        if (experience > MaximumExperience) throw new ArgumentOutOfRangeException(nameof(experience));
        while (level < MaximumLevel && experience >= Costs[level + 1])
        {
            experience -= Costs[++level];
        }
        return (level, experience);
    }
}
