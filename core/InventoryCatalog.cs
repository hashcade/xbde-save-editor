using System.Globalization;

namespace XbdeEditor.Core;

public enum InventoryKind { Collectables = 10, Materials, KeyItems, ArtManuals }

public sealed record InventoryDefinition(int Id, InventoryKind Kind, string Name);

public static class InventoryCatalog
{
    public const int Capacity = 500;
    public const int MaximumQuantity = 99;
    public static IReadOnlyList<InventoryDefinition> Definitions { get; } = Load();
    private static readonly IReadOnlyDictionary<int, InventoryDefinition> ById = Definitions.ToDictionary(item => item.Id);
    public static InventoryDefinition? Find(int id) => ById.GetValueOrDefault(id);
    internal static int Offset(InventoryKind kind) => kind switch
    {
        InventoryKind.Collectables => 0x31970,
        InventoryKind.Materials => 0x34080,
        InventoryKind.KeyItems => 0x36790,
        InventoryKind.ArtManuals => 0x38ea0,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static IReadOnlyList<InventoryDefinition> Load()
    {
        using var stream = typeof(InventoryCatalog).Assembly.GetManifestResourceStream("XbdeEditor.Core.Data.inventory.tsv")
            ?? throw new InvalidDataException("Missing inventory catalog.");
        using var reader = new StreamReader(stream);
        reader.ReadLine();
        var items = new List<InventoryDefinition>();
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split('\t');
            int id = int.Parse(fields[0], CultureInfo.InvariantCulture);
            var kind = (InventoryKind)int.Parse(fields[1], CultureInfo.InvariantCulture);
            if (id is < 1 or > ushort.MaxValue || !Enum.IsDefined(kind) || string.IsNullOrWhiteSpace(fields[2]))
                throw new InvalidDataException("Invalid inventory definition.");
            items.Add(new(id, kind, fields[2]));
        }
        return items.AsReadOnly();
    }
}
