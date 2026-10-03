namespace XbdeEditor.Core;

public sealed class InventoryEquipmentRecord
{
    private readonly SaveDocument _document;
    internal InventoryEquipmentRecord(SaveDocument document, EquipmentSlot slot, int index)
    { _document = document; Slot = slot; Index = index; }

    public EquipmentSlot Slot { get; }
    public int Index { get; }
    private int Offset => EquipmentCatalog.InventoryOffset(Slot) + Index * EquipmentRecord.InventorySize;
    public bool Exists => _document.ReadByte(Offset + 0x10) != 0;
    public int ItemId => _document.ReadUInt16(Offset + 4);
    public string Name => EquipmentCatalog.ItemName(ItemId);
    public int GemSlotCount => _document.ReadByte(Offset + 0x15);
    public int ArmorClass => _document.ReadByte(Offset + 0x14);
    public bool Favorite => _document.ReadByte(Offset + 0x11) == 1;
    public EquipmentDefinition? Definition => EquipmentDefinitions.Find(ItemId);
    public IReadOnlyList<int> FixedGemIds => Enumerable.Range(0, Math.Min(GemSlotCount, 3))
        .Where(socket => _document.ReadUInt32(Offset + 0x1c + socket * 8) != 0)
        .Select(socket => (int)_document.ReadUInt16(Offset + 0x1c + socket * 8)).ToArray();
    public IReadOnlyList<int> EquippedBy => _document.Characters.Where(character => character.GetEquipment(Slot).Index == Index)
        .Select(character => character.Id).ToArray();
    public bool CanEditFavorite => _document.CanEditInventoryEquipment && IsValid;
    public bool CanDelete => CanEditFavorite && Definition?.IsOrdinary == true
        && !_document.ReferencedEquipmentIndices(Slot).Contains(Index);

    public void SetFavorite(bool favorite)
    {
        if (!CanEditFavorite) throw new ArgumentException("This equipment record cannot be edited.");
        _document.WriteByte(Offset + 0x11, favorite ? (byte)1 : (byte)0);
    }

    public void Delete()
    {
        if (!CanDelete) throw new ArgumentException("Referenced, protected or malformed equipment cannot be deleted.");
        _document.WriteByte(Offset + 0x10, 0);
    }
    public bool IsValid => _document.ReadByte(Offset + 0x10) == 1
        && _document.ReadUInt16(Offset) == Index && _document.ReadUInt16(Offset + 2) == EquipmentCatalog.Type(Slot)
        && _document.ReadUInt16(Offset + 6) == EquipmentCatalog.Type(Slot) && _document.ReadUInt16(Offset + 8) == 1
        && GemSlotCount <= 3 && EquipmentRules.Get(ItemId) is { } rule && rule.Slot == Slot
        && (Slot == EquipmentSlot.Weapon || ArmorClass == rule.ArmorClass);
}

internal sealed record EquipmentRule(EquipmentSlot Slot, int ArmorClass, int WeaponFlags, int[] Characters);

internal static class EquipmentRules
{
    private static readonly IReadOnlyDictionary<int, EquipmentRule> Rules = Load();
    internal static EquipmentRule? Get(int itemId) => Rules.GetValueOrDefault(itemId);

    internal static bool CanUse(SaveDocument document, int characterId, InventoryEquipmentRecord item)
    {
        if (!item.IsValid || document.Campaign == Campaign.Unknown) return false;
        var rule = Rules[item.ItemId];
        if (!rule.Characters.Contains(characterId)) return false;
        if (item.Slot == EquipmentSlot.Weapon)
            return rule.WeaponFlags == 0 || characterId == 8 && rule.WeaponFlags == 3;
        if (rule.ArmorClass is 0 or 1) return true;
        if (characterId == 8) return rule.ArmorClass is >= 4 and <= 13;
        if (document.Campaign == Campaign.FutureConnected) return rule.ArmorClass is 2 or 3;
        if (characterId == 3 && rule.ArmorClass == 3) return false;
        int[] permissions = rule.ArmorClass switch
        {
            2 => [1, 26, 51, 76, 101],
            3 => [31, 98, 115, 141],
            _ => []
        };
        return permissions.Any(skill => HasSkill(document, characterId, skill));
    }

    private static bool HasSkill(SaveDocument document, int characterId, int skillId)
    {
        int owner = (skillId - 1) / 25 + 1;
        var donor = document.Characters.FirstOrDefault(character => character.Id == owner);
        var tree = donor?.SkillTrees.FirstOrDefault(branch => branch.Skills.Any(skill => skill.Id == skillId));
        int learned = (skillId - 1) % 5 + 1;
        if (tree is null || !tree.IsUnlocked || tree.LearnedCount < learned || tree.LearnedCount > 5) return false;
        if (owner == characterId) return true;
        int offset = SaveDocument.CharacterOffset + (characterId - 1) * SaveDocument.CharacterSize + 0xc4 + (owner - 1) * 5;
        return Enumerable.Range(0, 5).Any(index => document.ReadByte(offset + index) == skillId);
    }

    private static IReadOnlyDictionary<int, EquipmentRule> Load()
    {
        using var stream = typeof(EquipmentRules).Assembly.GetManifestResourceStream("XbdeEditor.Core.Data.equipment-rules.tsv")
            ?? throw new InvalidDataException("Missing equipment rules.");
        using var reader = new StreamReader(stream);
        reader.ReadLine();
        var rules = new Dictionary<int, EquipmentRule>();
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split('\t');
            rules.Add(Number(fields[0]), new EquipmentRule((EquipmentSlot)Number(fields[1]),
                Number(fields[2]), Number(fields[3]),
                fields[4].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Number).ToArray()));
        }
        if (rules.Count != 1572 || rules.Values.Any(rule => !Enum.IsDefined(rule.Slot) || rule.ArmorClass is < 0 or > 13))
            throw new InvalidDataException("Invalid equipment rules.");
        return rules;
    }

    private static int Number(string value) => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
