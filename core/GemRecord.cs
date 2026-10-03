namespace XbdeEditor.Core;

public sealed class GemRecord
{
    private readonly SaveDocument _document;
    internal const int InventoryOffset = 0x2c380;
    internal const int Size = 0x2c;
    private int Offset => InventoryOffset + Index * Size;
    internal GemRecord(SaveDocument document, int index) { _document = document; Index = index; }
    public int Index { get; }
    public bool Exists => _document.ReadByte(Offset + 0x10) == 1;
    public int Rank => _document.ReadByte(Offset + 0x16);
    public int EffectId => _document.ReadUInt16(Offset + 0x1c);
    public int Value => _document.ReadUInt16(Offset + 0x1e);
    public int Strength => Value & 0xff;
    public int Chance => Value >> 8;
    public bool IsCylinder => _document.ReadByte(Offset + 0x19) != 0;
    public GemDefinition? Definition => GemCatalog.Find(EffectId, Rank);
    public bool CanEdit => CanEquip && Definition is not null && _document.Campaign != Campaign.Unknown;
    public IReadOnlyList<GemDefinition> AvailableDefinitions
    {
        get
        {
            var slots = _document.GemEquipmentSlots(Index);
            return GemCatalog.Definitions.Where(rule => slots.All(rule.Allows)).ToArray();
        }
    }
    public bool HasValidValue => Definition is { } rule && Strength >= rule.Minimum && Strength <= rule.Maximum && Chance == rule.Chance;
    public string Name => EquipmentCatalog.GemEffectName(EffectId);
    public string Label => $"{Name} {RankLabel} · {Definition?.Describe(Strength, Chance) ?? Value.ToString()}";
    private string RankLabel => Rank is >= 1 and <= 6 ? new[] { "I", "II", "III", "IV", "V", "VI" }[Rank - 1] : $"#{Rank}";
    public bool CanEquip => Exists && Rank is >= 1 and <= 6 && EquipmentCatalog.KnownGemEffect(EffectId)
        && _document.ReadUInt16(Offset) == Index && _document.ReadUInt16(Offset + 2) == 3
        && _document.ReadUInt16(Offset + 6) == 3 && _document.ReadUInt16(Offset + 8) == 1
        && !IsCylinder && _document.ReadUInt16(Offset + 0x1a) == 1;

    public bool CanFit(EquipmentSlot slot) => Enum.IsDefined(slot) && CanEquip && Definition?.Allows(slot) == true;

    public void Set(int effectId, int rank, int strength)
    {
        if (!CanEdit) throw new ArgumentException("This record is not an editable normal gem.");
        var rule = GemCatalog.Find(effectId, rank) ?? throw new ArgumentException("Unrecognized gem effect or rank.");
        if (strength < rule.Minimum || strength > rule.Maximum)
            throw new ArgumentOutOfRangeException(nameof(strength), $"Allowed value: {rule.Minimum}–{rule.Maximum}.");
        int encoded = strength | (rule.Chance << 8);
        if (effectId == EffectId && rank == Rank && encoded == Value) return;
        if (!_document.GemEquipmentSlots(Index).All(rule.Allows))
            throw new ArgumentException("This gem effect is incompatible with its equipment. Remove it before changing the effect.");
        // Changed definitions use the game's crafted-gem form rather than an unrelated fixed item ID.
        if (effectId != EffectId || rank != Rank) _document.WriteUInt16(Offset + 4, 0);
        _document.WriteByte(Offset + 0x16, (byte)rank);
        _document.WriteByte(Offset + 0x17, (byte)rule.Attribute);
        _document.WriteUInt16(Offset + 0x1c, (ushort)effectId);
        _document.WriteUInt16(Offset + 0x1e, (ushort)encoded);
    }

    public void Maximize()
    {
        if (Definition is not { } rule) throw new ArgumentException("Unrecognized gem effect or rank.");
        Set(EffectId, Rank, rule.Maximum);
    }
}
