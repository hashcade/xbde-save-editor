namespace XbdeEditor.Core;

public sealed class CollectopaediaRecord
{
    internal const int FlagsOffset = 0x148d6c;
    internal const int FirstFlag = 0x19e9;
    private readonly SaveDocument _document;

    internal CollectopaediaRecord(SaveDocument document, CollectopaediaDefinition definition)
    {
        _document = document;
        Definition = definition;
    }

    public CollectopaediaDefinition Definition { get; }
    public int Id => Definition.Id;
    public int ItemId => Definition.ItemId;
    public string Name => Definition.Name;
    public int MapId => Definition.MapId;
    public string MapName => Definition.MapName;
    public int Category => Definition.Category;
    public bool IsRegistered => (_document.ReadByte(FlagsOffset + (FirstFlag + Id) / 8)
        & (1 << ((FirstFlag + Id) % 8))) != 0;
}
