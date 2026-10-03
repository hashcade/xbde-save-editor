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
    public string Name => EquipmentCatalog.GemEffectName(EffectId);
    public string Label => $"{Name} {RankLabel}";
    private string RankLabel => Rank is >= 1 and <= 6 ? new[] { "I", "II", "III", "IV", "V", "VI" }[Rank - 1] : $"#{Rank}";
    public bool CanEquip => Exists && Rank is >= 1 and <= 6 && EquipmentCatalog.KnownGemEffect(EffectId)
        && _document.ReadUInt16(Offset) == Index && _document.ReadUInt16(Offset + 2) == 3
        && _document.ReadUInt16(Offset + 6) == 3 && _document.ReadUInt16(Offset + 8) == 1
        && _document.ReadByte(Offset + 0x19) == 0 && _document.ReadUInt16(Offset + 0x1a) == 1;
}
