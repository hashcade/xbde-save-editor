namespace XbdeEditor.Core;

public sealed record CollectopaediaCompletionPlan(IReadOnlyList<CollectopaediaDefinition> NewEntries,
    IReadOnlyList<CollectopaediaRewardDefinition> Rewards, IReadOnlyList<int> NewAchievementIds);

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
        var pages = CollectopaediaCatalog.Pages.Where(page => page.Campaign == Campaign).ToArray();
        foreach (var page in pages)
        {
            foreach (var category in page.Categories)
                if (!category.Entries.All(entry => registered.Contains(entry.Id))
                    && category.Entries.All(entry => after.Contains(entry.Id))) rewards.Add(category.Reward);
            if (!page.Entries.All(entry => registered.Contains(entry.Id))
                && page.Entries.All(entry => after.Contains(entry.Id))) rewards.Add(page.Reward);
        }
        var achievements = new List<int>();
        if (Campaign == Campaign.MainStory && newEntries.Length != 0)
        {
            if (!GetAchievement(132).Completed) achievements.Add(132);
            bool[] complete = pages.Select(page => page.Entries.All(entry => after.Contains(entry.Id))).ToArray();
            if (complete.Any(value => value) && !GetAchievement(133).Completed) achievements.Add(133);
            if (complete.All(value => value) && !GetAchievement(134).Completed) achievements.Add(134);
        }
        return new(Array.AsReadOnly(newEntries), rewards.AsReadOnly(), achievements.AsReadOnly());
    }

    public CollectopaediaCompletionPlan CompleteCollectopaedia(IEnumerable<int> entryIds)
    {
        var plan = PlanCollectopaediaCompletion(entryIds);
        if (plan.NewEntries.Count == 0) return plan;

        // Stage the entire batch, including all inventory banks, before touching this document.
        var staged = Parse(_data);
        var equipment = plan.Rewards.Where(reward => reward.Equipment is not null)
            .Select(reward => reward.Equipment!).ToArray();
        staged.CreateEquipmentBatch(equipment);
        foreach (var reward in plan.Rewards.Where(reward => reward.Gem is not null))
        {
            var rule = reward.Gem!;
            int strength = reward.FixedStrength != 0 ? reward.FixedStrength
                : Random.Shared.Next(rule.Minimum, rule.Maximum + 1);
            var gem = staged.AddGem(reward.EffectId, reward.Rank, strength);
            staged.WriteUInt16(GemRecord.InventoryOffset + gem.Index * GemRecord.Size + 4, (ushort)reward.ItemId);
        }
        foreach (var entry in plan.NewEntries)
        {
            int bit = CollectopaediaRecord.FirstFlag + entry.Id;
            int offset = CollectopaediaRecord.FlagsOffset + bit / 8;
            staged.WriteByte(offset, (byte)(staged.ReadByte(offset) | (1 << (bit % 8))));
        }
        // Match the standalone achievement editor: unlock records without changing character EXP.
        foreach (int id in plan.NewAchievementIds) staged.GetAchievement(id).Unlock();
        staged._data.CopyTo(_data, 0);
        return plan;
    }
}
