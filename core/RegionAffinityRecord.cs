namespace XbdeEditor.Core;

public sealed record RegionAffinityDefinition(int Id, string Name);

public sealed class RegionAffinityRecord
{
    public const int MaximumPoints = 10_000;
    public const int FiveStarPoints = 8_000;
    public static IReadOnlyList<RegionAffinityDefinition> Definitions { get; } = Array.AsReadOnly<RegionAffinityDefinition>(
    [
        new(1, "Colony 9 Area"), new(2, "Colony 6 Area"), new(3, "Central Bionis"),
        new(4, "Upper Bionis"), new(5, "Hidden Village")
    ]);
    private readonly SaveDocument _document;

    internal RegionAffinityRecord(SaveDocument document, RegionAffinityDefinition definition)
    {
        _document = document;
        Definition = definition;
    }

    public RegionAffinityDefinition Definition { get; }
    public int Id => Definition.Id;
    public int Points => _document.ReadUInt16(0xdf2 + Id * 2);
    public int Stars => Math.Min(Points / 2_000 + 1, 5);
    public bool CanEdit => _document.CanEditRegionAffinity;
    public bool IsMaximum => Points == MaximumPoints;

    public void SetPoints(int points)
    {
        if (!CanEdit) throw new ArgumentException("Region affinity requires an identified main-story save with format version 7.");
        if (points is < 0 or > MaximumPoints) throw new ArgumentOutOfRangeException(nameof(points));
        _document.WriteUInt16(0xdf2 + Id * 2, (ushort)points);
    }

    public void SetStars(int stars)
    {
        if (!CanEdit) throw new ArgumentException("Region affinity requires an identified main-story save with format version 7.");
        if (stars is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(stars));
        if (stars != Stars) SetPoints((stars - 1) * 2_000);
    }
}
