using System.Globalization;

namespace XbdeEditor.Core;

public enum AchievementCategory { Trials = 1, Records = 2 }

public sealed record AchievementDefinition(int Id, string Name, AchievementCategory Category,
    int Order, string Condition, int RewardExperience, int Required, int ConditionType)
{
    public bool HasCounter => ConditionType is 3 or 4;
}

public static class AchievementCatalog
{
    public static IReadOnlyList<AchievementDefinition> All { get; } = Load();

    public static AchievementDefinition Get(int id) => All.FirstOrDefault(item => item.Id == id)
        ?? throw new ArgumentOutOfRangeException(nameof(id));

    private static IReadOnlyList<AchievementDefinition> Load()
    {
        using var stream = typeof(AchievementCatalog).Assembly.GetManifestResourceStream("XbdeEditor.Core.Data.achievements.tsv")
            ?? throw new InvalidDataException("Missing achievement catalog.");
        using var reader = new StreamReader(stream);
        reader.ReadLine();
        var definitions = new List<AchievementDefinition>();
        while (reader.ReadLine() is { } line)
        {
            string[] fields = line.Split('\t');
            int Number(int index) => int.Parse(fields[index], CultureInfo.InvariantCulture);
            if (fields.Length != 8) throw new InvalidDataException("Invalid achievement catalog row.");
            definitions.Add(new(Number(0), fields[1], (AchievementCategory)Number(2), Number(3),
                fields[4], Number(5), Number(6), Number(7)));
        }
        if (!definitions.Select(item => item.Id).Order().SequenceEqual(Enumerable.Range(1, 200))
            || definitions.Any(item => !Enum.IsDefined(item.Category) || item.ConditionType is < 0 or > 4
                || item.Required < 0 || item.HasCounter && item.Required > ushort.MaxValue))
            throw new InvalidDataException("Invalid achievement catalog definitions.");
        return definitions.AsReadOnly();
    }
}
