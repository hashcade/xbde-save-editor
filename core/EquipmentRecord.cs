namespace XbdeEditor.Core;

public sealed class EquipmentRecord
{
    internal const int InventorySize = 0x30;
    private readonly SaveDocument _document;
    private int CharacterOffset => SaveDocument.CharacterOffset + (CharacterId - 1) * SaveDocument.CharacterSize;
    private int ReferenceOffset => CharacterOffset + EquipmentCatalog.CharacterField(Slot);
    internal int Offset => EquipmentCatalog.InventoryOffset(Slot) + Index * InventorySize;
    internal EquipmentRecord(SaveDocument document, int characterId, EquipmentSlot slot)
    { _document = document; CharacterId = characterId; Slot = slot; }

    public int CharacterId { get; }
    public EquipmentSlot Slot { get; }
    public int Index => _document.ReadUInt16(ReferenceOffset);
    public bool Exists => Index < 500 && _document.ReadUInt16(ReferenceOffset + 2) == EquipmentCatalog.Type(Slot)
        && _document.ReadByte(Offset + 0x10) == 1 && _document.ReadUInt16(Offset) == Index
        && _document.ReadUInt16(Offset + 2) == EquipmentCatalog.Type(Slot)
        && _document.ReadUInt16(Offset + 6) == EquipmentCatalog.Type(Slot)
        && _document.ReadUInt16(Offset + 8) == 1;
    public int? ItemId => Exists ? _document.ReadUInt16(Offset + 4) : null;
    public string Name => ItemId is { } id ? EquipmentCatalog.ItemName(id) : $"Unknown reference ({Index}, {_document.ReadUInt16(ReferenceOffset + 2)})";
    public int GemSlotCount => Exists ? _document.ReadByte(Offset + 0x15) : 0;
    public IReadOnlyList<GemSocketRecord> GemSockets => Exists
        ? Enumerable.Range(1, Math.Min(GemSlotCount, 3)).Select(index => new GemSocketRecord(this, _document, index)).ToArray() : [];
    public bool CanEdit => Exists && GemSlotCount <= 3 && _document.Campaign != Campaign.Unknown
        && _document.Characters.Count(character => character.Equipment.Any(equipment => equipment.Slot == Slot && equipment.Index == Index && equipment.Exists)) == 1;

    public void SetGems(IReadOnlyList<int?> indices)
    {
        if (!CanEdit || indices.Count != GemSlotCount)
            throw new ArgumentException("Only valid equipped items and their existing sockets can be edited.");
        var sockets = GemSockets;
        var changes = new List<(GemSocketRecord Socket, int? Index)>();
        for (int index = 0; index < sockets.Count; index++)
        {
            var socket = sockets[index];
            if (socket.GemIndex == indices[index]) continue;
            if (!socket.CanEdit) throw new ArgumentException("Fixed or unrecognized gems cannot be replaced.");
            if (indices[index] is { } gemIndex)
            {
                if (gemIndex is < 0 or >= 500 || !_document.GetGem(gemIndex).CanEquip)
                    throw new ArgumentException("Choose an existing, valid gem from inventory.");
                if (_document.IsGemUsed(gemIndex, this))
                    throw new ArgumentException("This gem is already fitted to another item.");
            }
            changes.Add((socket, indices[index]));
        }
        var selected = indices.Where(index => index is not null).ToArray();
        if (changes.Count > 0 && selected.Distinct().Count() != selected.Length)
            throw new ArgumentException("One gem cannot occupy multiple sockets.");
        foreach (var (socket, index) in changes)
            _document.WriteUInt32(socket.Offset, index is { } value ? (uint)value | (3u << 16) : 0);
    }
}

public sealed class GemSocketRecord
{
    private readonly EquipmentRecord _equipment;
    private readonly SaveDocument _document;
    internal GemSocketRecord(EquipmentRecord equipment, SaveDocument document, int index)
    { _equipment = equipment; _document = document; Index = index; }
    public int Index { get; }
    internal int Offset => _equipment.Offset + 0x18 + (Index - 1) * 8;
    public int? GemIndex => _document.ReadUInt16(Offset + 2) == 3 ? _document.ReadUInt16(Offset) : null;
    public int? FixedItemId => _document.ReadUInt32(Offset + 4) != 0 ? _document.ReadUInt16(Offset + 4) : null;
    public bool IsEmpty => _document.ReadUInt32(Offset) == 0 && FixedItemId is null;
    public bool CanEdit => _equipment.CanEdit && FixedItemId is null
        && (IsEmpty || GemIndex is { } index && index < 500 && _document.GetGem(index).CanEquip);
    public string? Name
    {
        get
        {
            if (FixedItemId is { } fixedId) return EquipmentCatalog.ItemName(fixedId);
            if (GemIndex is { } index && index < 500 && _document.GetGem(index).Exists)
                return _document.GetGem(index).Label;
            if (IsEmpty) return null;
            return $"Unknown gem ({_document.ReadUInt16(Offset)}, {_document.ReadUInt16(Offset + 2)})";
        }
    }
    public IReadOnlyList<GemRecord> AvailableGems
    {
        get
        {
            var used = _document.UsedGemIndices();
            return _document.Gems.Where(gem => gem.CanEquip && (gem.Index == GemIndex || !used.Contains(gem.Index))).ToArray();
        }
    }
}
