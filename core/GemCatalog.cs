using System.Globalization;

namespace XbdeEditor.Core;

public sealed record GemDefinition(int EffectId, int Rank, int Minimum, int Maximum, int Chance, int Attribute, int ValueType, int Attachment)
{
    public string Name => EquipmentCatalog.GemEffectName(EffectId);
    public string Unit => ValueType == 3 ? "%" : "";
    public bool Allows(EquipmentSlot slot) => Attachment == 0 || (slot == EquipmentSlot.Weapon ? Attachment == 1 : Attachment == 2);
    public string Describe(int strength, int chance)
    {
        string value = $"{strength}{Unit}";
        if (Maximum == 0) return $"{chance}%";
        return chance == 0 ? value : $"{value} / {chance}%";
    }
}

public static class GemCatalog
{
    public static IReadOnlyList<GemDefinition> Definitions { get; } = Load();
    public static GemDefinition? Find(int effectId, int rank) =>
        Definitions.FirstOrDefault(rule => rule.EffectId == effectId && rule.Rank == rank);

    private static IReadOnlyList<GemDefinition> Load()
    {
        using var stream = typeof(GemCatalog).Assembly.GetManifestResourceStream("XbdeEditor.Core.Data.gem-rules.tsv")
            ?? throw new InvalidDataException("Missing gem rules.");
        using var reader = new StreamReader(stream);
        reader.ReadLine();
        var rules = new List<GemDefinition>();
        while (reader.ReadLine() is { } line)
        {
            var values = line.Split('\t').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            rules.Add(new GemDefinition(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]));
        }
        return rules.AsReadOnly();
    }
}
