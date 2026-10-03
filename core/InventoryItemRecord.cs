namespace XbdeEditor.Core;

public sealed class InventoryItemRecord
{
    internal const int Size = 0x14;
    private readonly SaveDocument _document;
    internal InventoryItemRecord(SaveDocument document, InventoryKind kind, int index)
    { _document = document; Kind = kind; Index = index; }
    public InventoryKind Kind { get; }
    public int Index { get; }
    internal int Offset => InventoryCatalog.Offset(Kind) + Index * Size;
    public bool Exists => _document.ReadByte(Offset + 0x10) == 1;
    public int ItemId => _document.ReadUInt16(Offset + 4);
    public int Quantity => _document.ReadUInt16(Offset + 8);
    public bool Favorite => _document.ReadByte(Offset + 0x11) == 1;
    public InventoryDefinition? Definition => InventoryCatalog.Find(ItemId);
    public string Name => Definition?.Name ?? $"Item #{ItemId}";
    public bool CanEdit => _document.Campaign != Campaign.Unknown && Kind != InventoryKind.KeyItems
        && Exists && Definition?.Kind == Kind && _document.ReadUInt16(Offset) == Index
        && _document.ReadUInt16(Offset + 2) == (int)Kind && _document.ReadUInt16(Offset + 6) == (int)Kind;

    public void SetQuantity(int quantity)
    {
        RequireEditable();
        if (quantity is < 1 or > InventoryCatalog.MaximumQuantity)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Allowed quantity: 1–99. Use Delete to remove an item.");
        _document.WriteUInt16(Offset + 8, (ushort)quantity);
    }

    public void SetFavorite(bool favorite)
    {
        RequireEditable();
        _document.WriteByte(Offset + 0x11, favorite ? (byte)1 : (byte)0);
    }

    public void Delete()
    {
        RequireEditable();
        // The game marks a record absent without shifting any inventory indices.
        _document.WriteByte(Offset + 0x10, 0);
    }

    private void RequireEditable()
    {
        if (!CanEdit) throw new ArgumentException("This inventory record is not editable.");
    }
}
