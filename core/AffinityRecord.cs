namespace XbdeEditor.Core;

public sealed class AffinityRecord
{
    private readonly SaveDocument _document;
    private readonly AffinityPairDefinition _definition;
    internal AffinityRecord(SaveDocument document, int firstId, int secondId)
    {
        _document = document;
        _definition = AffinityCatalog.Get(firstId, secondId);
        FirstCharacterId = firstId;
        SecondCharacterId = secondId;
    }

    public int Index => _definition.Index;
    public int FirstCharacterId { get; }
    public int SecondCharacterId { get; }
    public int Points => _document.ReadUInt16(0xe00 + Index * 2);
    public bool CanEdit => _document.CanEditAffinity;
    public int? FirstUnlockedSlots => SlotCount(ReadUnlockIndex(FirstCharacterId, SecondCharacterId));
    public int? SecondUnlockedSlots => SlotCount(ReadUnlockIndex(SecondCharacterId, FirstCharacterId));
    public bool IsMaximum => Points == AffinityCatalog.MaximumPoints
        && DirectedSlots().All(slot => ReadUnlockIndex(slot.Recipient, slot.Source) >= 4);

    public void SetPoints(int points)
    {
        if (!CanEdit) throw new ArgumentException("Affinity requires an identified main-story save with format version 7.");
        if (points is < 0 or > AffinityCatalog.MaximumPoints) throw new ArgumentOutOfRangeException(nameof(points));
        _document.WriteUInt16(0xe00 + Index * 2, (ushort)points);
        uint unlockedIndex = (uint)Math.Min(points / 1_000, 4);
        // Native slot unlocks only increase. Both Fiora forms retain their own directed rows.
        foreach (var slot in DirectedSlots())
        {
            int offset = UnlockOffset(slot.Recipient, slot.Source);
            _document.WriteUInt32(offset, Math.Max(_document.ReadUInt32(offset), unlockedIndex));
        }
    }

    private IEnumerable<(int Recipient, int Source)> DirectedSlots()
    {
        int[] first = Aliases(_definition.FirstCharacterId), second = Aliases(_definition.SecondCharacterId);
        foreach (int recipient in first)
            foreach (int source in second)
            {
                yield return (recipient, source);
                yield return (source, recipient);
            }
    }

    private static int[] Aliases(int canonicalId) => canonicalId == 3 ? [3, 8] : [canonicalId];
    private static int? SlotCount(uint index) => index <= 4 ? (int)index + 1 : null;
    private uint ReadUnlockIndex(int recipient, int source) => _document.ReadUInt32(UnlockOffset(recipient, source));
    private static int UnlockOffset(int recipient, int source) => SaveDocument.CharacterOffset
        + (recipient - 1) * SaveDocument.CharacterSize + 0xa4 + (source - 1) * 4;
}
