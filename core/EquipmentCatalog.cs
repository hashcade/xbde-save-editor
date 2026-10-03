using System.Globalization;

namespace XbdeEditor.Core;

public enum EquipmentSlot { Weapon, Head, Torso, Arms, Legs, Feet }

public static class EquipmentCatalog
{
    private static readonly IReadOnlyDictionary<int, string> Items = Load("equipment.tsv");
    private static readonly IReadOnlyDictionary<int, string> Effects = Load("gem-effects.tsv");
    public static string ItemName(int id) => Items.GetValueOrDefault(id) is { Length: > 0 } name ? name : $"Item #{id}";
    public static string GemEffectName(int id) => Effects.GetValueOrDefault(id) is { Length: > 0 } name ? name : $"Effect #{id}";
    internal static bool KnownGemEffect(int id) => Effects.ContainsKey(id);
    internal static int Type(EquipmentSlot slot) => slot == EquipmentSlot.Weapon ? 2 : (int)slot + 3;
    internal static int InventoryOffset(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Weapon => 0x3b10,
        EquipmentSlot.Head => 0x98d0,
        EquipmentSlot.Torso => 0xf690,
        EquipmentSlot.Arms => 0x15450,
        EquipmentSlot.Legs => 0x1b210,
        EquipmentSlot.Feet => 0x20fd0,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
    internal static int CharacterField(EquipmentSlot slot) => slot == EquipmentSlot.Weapon ? 0x28 : 0x10 + (int)slot * 4;

    private static IReadOnlyDictionary<int, string> Load(string filename)
    {
        using var stream = typeof(EquipmentCatalog).Assembly.GetManifestResourceStream($"XbdeEditor.Core.Data.{filename}")
            ?? throw new InvalidDataException($"Missing equipment catalog: {filename}");
        using var reader = new StreamReader(stream);
        reader.ReadLine();
        var names = new Dictionary<int, string>();
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split('\t');
            names.Add(int.Parse(fields[0], CultureInfo.InvariantCulture), fields[1]);
        }
        return names;
    }
}
