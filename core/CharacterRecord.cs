namespace XbdeEditor.Core;

public sealed class CharacterRecord
{
    private readonly SaveDocument _document;
    private readonly int _offset;

    internal CharacterRecord(SaveDocument document, int id)
    {
        _document = document;
        Id = id;
        _offset = SaveDocument.CharacterOffset + (id - 1) * SaveDocument.CharacterSize;
    }

    public int Id { get; }
    public uint Level => _document.ReadUInt32(_offset);
    public uint Experience => _document.ReadUInt32(_offset + 4);
    public uint AP => _document.ReadUInt32(_offset + 8);
    public uint AffinityCoins => _document.ReadUInt32(_offset + 12);
    public uint ExpertLevel => _document.ReadUInt32(_offset + 0xec);
    public uint ExpertExperience => _document.ReadUInt32(_offset + 0xf0);
    public uint ReserveExperience => _document.ReadUInt32(_offset + 0xf4);
    public bool UsesAffinityCoins => _document.Campaign == Campaign.MainStory;

    public void SetResources(uint? ap = null, uint? affinityCoins = null)
    {
        if (affinityCoins is > 999)
            throw new ArgumentOutOfRangeException(nameof(affinityCoins), "Affinity Coins must be between 0 and 999.");
        if (affinityCoins is not null && !UsesAffinityCoins)
            throw new ArgumentException("Affinity Coins can only be edited in an identified main-story save.");
        if (ap is { } newAP) _document.WriteUInt32(_offset + 8, newAP);
        if (affinityCoins is { } newCoins) _document.WriteUInt32(_offset + 12, newCoins);
    }
}
