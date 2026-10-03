using System.Globalization;

namespace XbdeEditor.Core;

public sealed record CollectopaediaDefinition(int Id, int ItemId, int MapId, string MapName, int Category)
{
    public string Name => InventoryCatalog.Find(ItemId)!.Name;
    public Campaign Campaign => Id <= 300 ? Campaign.MainStory : Campaign.FutureConnected;
}

public sealed record CollectopaediaRewardDefinition(int Id, int ItemId, string Name, int ItemType,
    int EffectId, int Rank, int FixedStrength)
{
    public GemDefinition? Gem => ItemType == 3 ? GemCatalog.Find(EffectId, Rank) : null;
    public EquipmentDefinition? Equipment => ItemType == 3 ? null : EquipmentDefinitions.Find(ItemId);
}
public sealed record CollectopaediaCategoryDefinition(int Type, IReadOnlyList<CollectopaediaDefinition> Entries,
    CollectopaediaRewardDefinition Reward);
public sealed record CollectopaediaPageDefinition(int MapId, string MapName, Campaign Campaign,
    IReadOnlyList<CollectopaediaDefinition> Entries, IReadOnlyList<CollectopaediaCategoryDefinition> Categories,
    CollectopaediaRewardDefinition Reward);

public static class CollectopaediaCatalog
{
    public static IReadOnlyList<CollectopaediaDefinition> All { get; } = LoadEntries();
    public static IReadOnlyList<CollectopaediaRewardDefinition> Rewards { get; } = LoadRewards();
    public static IReadOnlyList<CollectopaediaPageDefinition> Pages { get; } = LoadPages();

    private static IReadOnlyList<CollectopaediaPageDefinition> LoadPages()
    {
        // Native map IDs 2–30 index this table; absent category types do not reserve reward rows.
        int[] firstReward = [1, 8, 15, 22, 26, 32, 39, 46, 1, 50, 54, 60, 1, 64, 71,
            78, 84, 88, 1, 95, 1, 102, 109, 116, 120, 1, 1, 124, 129];
        var pages = new List<CollectopaediaPageDefinition>();
        foreach (var map in All.GroupBy(entry => entry.MapId).OrderBy(group => group.Key))
        {
            var entries = map.OrderBy(entry => entry.Category).ThenBy(entry => entry.Id).ToArray();
            int rewardId = firstReward[map.Key - 2];
            var categories = entries.GroupBy(entry => entry.Category).Select((group, index) =>
                new CollectopaediaCategoryDefinition(group.Key, Array.AsReadOnly(group.ToArray()),
                    Rewards[rewardId + index])).ToArray();
            pages.Add(new(map.Key, entries[0].MapName, entries[0].Campaign, Array.AsReadOnly(entries),
                Array.AsReadOnly(categories), Rewards[rewardId - 1]));
        }
        var usedRewards = pages.SelectMany(page => page.Categories.Select(category => category.Reward.Id)
            .Append(page.Reward.Id)).Order().ToArray();
        if (!usedRewards.SequenceEqual(Enumerable.Range(1, Rewards.Count)))
            throw new InvalidDataException("Collectopaedia pages do not match the native reward table.");
        return pages.AsReadOnly();
    }

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
            new CollectopaediaRewardDefinition(Number(fields[0]), Number(fields[1]), fields[2], Number(fields[3]),
                Number(fields[4]), Number(fields[5]), Number(fields[6]))).ToArray();
        if (!rewards.Select(reward => reward.Id).SequenceEqual(Enumerable.Range(1, 133))
            || rewards.Any(reward => reward.ItemId <= 0 || string.IsNullOrWhiteSpace(reward.Name)))
            throw new InvalidDataException("Invalid Collectopaedia reward catalog.");
        foreach (var reward in rewards)
        {
            if (reward.ItemType == 3)
            {
                if (reward.Gem is not { } gem || reward.FixedStrength < 0
                    || (reward.FixedStrength != 0 && (reward.FixedStrength < gem.Minimum || reward.FixedStrength > gem.Maximum)))
                    throw new InvalidDataException("Unrecognized Collectopaedia gem reward.");
            }
            else if (reward.Equipment is not { } equipment
                || EquipmentCatalog.Type(equipment.Slot) != reward.ItemType
                || reward.EffectId != 0 || reward.Rank != 0 || reward.FixedStrength != 0)
                throw new InvalidDataException($"Unrecognized Collectopaedia equipment reward: {reward.Id} / item {reward.ItemId}.");
        }
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
