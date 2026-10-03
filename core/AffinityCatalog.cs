namespace XbdeEditor.Core;

public sealed record AffinityPairDefinition(int Index, int FirstCharacterId, int SecondCharacterId);

public static class AffinityCatalog
{
    public const int MaximumPoints = 5_000;
    public static IReadOnlyList<AffinityPairDefinition> All { get; } = Build();

    public static int CanonicalCharacterId(int id)
    {
        if (id is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(id));
        return id == 8 ? 3 : id;
    }

    public static AffinityPairDefinition Get(int firstId, int secondId)
    {
        int first = CanonicalCharacterId(firstId), second = CanonicalCharacterId(secondId);
        return All.FirstOrDefault(pair => pair.FirstCharacterId == Math.Min(first, second)
            && pair.SecondCharacterId == Math.Max(first, second))
            ?? throw new ArgumentException("A character cannot have affinity with itself or its alternate form.");
    }

    private static IReadOnlyList<AffinityPairDefinition> Build()
    {
        var pairs = new List<AffinityPairDefinition>();
        // Native matrix 0x9D46DC enumerates the 21 pairs in this order; Fiora 8 aliases 3.
        for (int first = 1; first <= 7; first++)
            for (int second = first + 1; second <= 7; second++)
                pairs.Add(new(pairs.Count + 1, first, second));
        return pairs.AsReadOnly();
    }
}
