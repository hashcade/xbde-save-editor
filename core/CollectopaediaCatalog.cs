using System.Globalization;

namespace XbdeEditor.Core;

public sealed record CollectopaediaDefinition(int Id, int ItemId, int MapId, string MapName, int Category)
{
    public string Name => InventoryCatalog.Find(ItemId)!.Name;
    public Campaign Campaign => Id <= 300 ? Campaign.MainStory : Campaign.FutureConnected;
}

public sealed record CollectopaediaRewardDefinition(int Id, int ItemId, string Name);

public static class CollectopaediaCatalog
{
    public static IReadOnlyList<CollectopaediaDefinition> All { get; } = LoadEntries();
    public static IReadOnlyList<CollectopaediaRewardDefinition> Rewards { get; } = LoadRewards();

    private static IReadOnlyList<CollectopaediaDefinition> LoadEntries()
    {
        var entries = ReadRows("collectopaedia.tsv").Select(fields => new CollectopaediaDefinition(
            Number(fields[0]), Number(fields[1]), Number(fields[2]), fields[3], Number(fields[4]))).ToArray();
        int[] expectedIds = Enumerable.Range(1, 300).Concat(Enumerable.Range(319, 28)).ToArray();
        if (!entries.Select(entry => entry.Id).SequenceEqual(expectedIds)
            || entries.Select(entry => entry.ItemId).Distinct().Count() != entries.Length
            || entries.Any(entry => entry.MapId is < 2 or > 30 || entry.Category is < 1 or > 8
                || InventoryCatalog.Find(entry.ItemId)?.Kind != InventoryKind.Collectables))
            throw new InvalidDataException("Invalid Collectopaedia catalog.");
        return Array.AsReadOnly(entries);
    }

    private static IReadOnlyList<CollectopaediaRewardDefinition> LoadRewards()
    {
        var rewards = ReadRows("collectopaedia-rewards.tsv").Select(fields =>
            new CollectopaediaRewardDefinition(Number(fields[0]), Number(fields[1]), fields[2])).ToArray();
        if (!rewards.Select(reward => reward.Id).SequenceEqual(Enumerable.Range(1, 133))
            || rewards.Any(reward => reward.ItemId <= 0 || string.IsNullOrWhiteSpace(reward.Name)))
            throw new InvalidDataException("Invalid Collectopaedia reward catalog.");
        return Array.AsReadOnly(rewards);
    }

    private static IEnumerable<string[]> ReadRows(string filename)
    {
        using var stream = typeof(CollectopaediaCatalog).Assembly.GetManifestResourceStream($"XbdeEditor.Core.Data.{filename}")
            ?? throw new InvalidDataException($"Missing Collectopaedia catalog: {filename}");
        using var reader = new StreamReader(stream);
        reader.ReadLine();
        while (reader.ReadLine() is { } line) yield return line.Split('\t');
    }

    private static int Number(string value) => int.Parse(value, CultureInfo.InvariantCulture);
}
