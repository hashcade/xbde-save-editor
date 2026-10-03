namespace XbdeEditor.Core;

public sealed record CollectopaediaCompletionPlan(IReadOnlyList<CollectopaediaDefinition> NewEntries,
    IReadOnlyList<CollectopaediaRewardDefinition> Rewards);

public sealed partial class SaveDocument
{
    public bool CanInspectCollectopaedia => Campaign != Campaign.Unknown && ReadUInt32(0) == 7;

    public IReadOnlyList<CollectopaediaRecord> Collectopaedia => CanInspectCollectopaedia
        ? CollectopaediaCatalog.All.Where(entry => entry.Campaign == Campaign)
            .Select(entry => new CollectopaediaRecord(this, entry)).ToArray() : [];

    public CollectopaediaRecord GetCollectopaediaEntry(int id) => Collectopaedia.FirstOrDefault(entry => entry.Id == id)
        ?? throw new ArgumentException("Choose a Collectopaedia entry from this campaign and save format 7.", nameof(id));

    public CollectopaediaCompletionPlan PlanCollectopaediaCompletion(IEnumerable<int> entryIds)
    {
        ArgumentNullException.ThrowIfNull(entryIds);
        if (!CanInspectCollectopaedia)
            throw new ArgumentException("Collectopaedia requires an identified campaign and save format 7.");
        var entries = Collectopaedia;
        var validIds = entries.Select(entry => entry.Id).ToHashSet();
        var selectedIds = entryIds.ToHashSet();
        if (!selectedIds.IsSubsetOf(validIds))
            throw new ArgumentException("Choose Collectopaedia entries from this campaign.", nameof(entryIds));
        var registered = entries.Where(entry => entry.IsRegistered).Select(entry => entry.Id).ToHashSet();
        var newEntries = entries.Where(entry => selectedIds.Contains(entry.Id) && !registered.Contains(entry.Id))
            .Select(entry => entry.Definition).ToArray();
        var after = registered.Union(newEntries.Select(entry => entry.Id)).ToHashSet();
        var rewards = new List<CollectopaediaRewardDefinition>();
        foreach (var page in CollectopaediaCatalog.Pages.Where(page => page.Campaign == Campaign))
        {
            foreach (var category in page.Categories)
                if (!category.Entries.All(entry => registered.Contains(entry.Id))
                    && category.Entries.All(entry => after.Contains(entry.Id))) rewards.Add(category.Reward);
            if (!page.Entries.All(entry => registered.Contains(entry.Id))
                && page.Entries.All(entry => after.Contains(entry.Id))) rewards.Add(page.Reward);
        }
        return new(Array.AsReadOnly(newEntries), rewards.AsReadOnly());
    }
}
