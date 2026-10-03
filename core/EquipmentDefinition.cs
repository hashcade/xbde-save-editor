using System.Globalization;

namespace XbdeEditor.Core;

public sealed record EquipmentDefinition(int Id, EquipmentSlot Slot, int ArmorClass, int GemSlotCount,
    int FixedGem1, int FixedGem2, int FixedGem3, int WeaponFlags, string Name, IReadOnlyList<int> Characters)
{
    public int FixedGem(int socket) => socket switch
    {
        1 => FixedGem1, 2 => FixedGem2, 3 => FixedGem3,
        _ => throw new ArgumentOutOfRangeException(nameof(socket))
    };

    public bool IsOrdinary => Name.Length > 0 && Name != Id.ToString(CultureInfo.InvariantCulture)
        && !Name.StartsWith("Item #", StringComparison.Ordinal)
        && !Name.Equals("Not Equipped", StringComparison.OrdinalIgnoreCase)
        && !Name.Contains("Dummy", StringComparison.OrdinalIgnoreCase)
        && (Slot == EquipmentSlot.Weapon ? WeaponFlags == 0 || WeaponFlags == 3 && Characters.Contains(8) : ArmorClass > 0);
}

public static class EquipmentDefinitions
{
    public static IReadOnlyList<EquipmentDefinition> All { get; } = Load();
    private static readonly IReadOnlyDictionary<int, EquipmentDefinition> ById = All.ToDictionary(item => item.Id);
    public static EquipmentDefinition? Find(int itemId) => ById.GetValueOrDefault(itemId);

    private static IReadOnlyList<EquipmentDefinition> Load()
    {
        using var stream = typeof(EquipmentDefinitions).Assembly.GetManifestResourceStream("XbdeEditor.Core.Data.equipment-initializers.tsv")
            ?? throw new InvalidDataException("Missing equipment initializers.");
        using var reader = new StreamReader(stream);
        reader.ReadLine();
        var definitions = new List<EquipmentDefinition>();
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split('\t').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            if (fields.Length != 5 || fields[0] is < 1 or > ushort.MaxValue || fields[1] is < 0 or > 3
                || fields.Skip(2).Any(value => value is < 0 or > ushort.MaxValue)
                || Enumerable.Range(fields[1] + 2, 3 - fields[1]).Any(index => fields[index] != 0))
                throw new InvalidDataException("Invalid equipment initializer.");
            var rule = EquipmentRules.Get(fields[0]) ?? throw new InvalidDataException("Equipment initializer has no eligibility rule.");
            definitions.Add(new(fields[0], rule.Slot, rule.ArmorClass, fields[1], fields[2], fields[3], fields[4],
                rule.WeaponFlags, EquipmentCatalog.ItemName(fields[0]), Array.AsReadOnly((int[])rule.Characters.Clone())));
        }
        if (definitions.Count != 1572 || definitions.Select(item => item.Id).Distinct().Count() != definitions.Count)
            throw new InvalidDataException("Incomplete equipment initializers.");
        return definitions.AsReadOnly();
    }
}
