using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class CollectopaediaTests
{
    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        check(CollectopaediaCatalog.All.Count == 328
            && CollectopaediaCatalog.All.All(entry => !string.IsNullOrWhiteSpace(entry.Name)),
            "Collectopaedia item references do not resolve to the inventory catalog.");
        check(CollectopaediaCatalog.Rewards.Count == 133
            && CollectopaediaCatalog.Rewards[0].ItemId == 283
            && CollectopaediaCatalog.Rewards[^1].ItemId == 3859, "Collectopaedia reward rows differ from the native table.");
        check(CollectopaediaCatalog.Pages.Count == 23
            && CollectopaediaCatalog.Pages.Sum(page => page.Categories.Count + 1) == 133,
            "Collectopaedia page/category rewards do not cover all native rows.");
        var colony9 = CollectopaediaCatalog.Pages.Single(page => page.MapId == 2);
        check(colony9.Categories.Select(category => category.Type).SequenceEqual(new[] { 1, 2, 3, 5, 7, 8 })
            && colony9.Categories.Select(category => category.Reward.Id).SequenceEqual(Enumerable.Range(2, 6))
            && colony9.Reward.Id == 1, "Absent category types incorrectly reserve reward rows.");
        var memorySpace = CollectopaediaCatalog.Pages.Single(page => page.MapId == 26);
        check(memorySpace.Reward.Id == 120 && memorySpace.Categories.Select(category => category.Reward.Id)
            .SequenceEqual(new[] { 121, 122, 123 }), "Memory Space rewards are mapped to another page.");
        var quickStep = CollectopaediaCatalog.Rewards.Single(reward => reward.Id == 2);
        check(quickStep.ItemId == 3371 && quickStep.ItemType == 3 && quickStep.EffectId == 77
            && quickStep.Rank == 3 && quickStep.FixedStrength == 10 && quickStep.Gem is not null,
            "Fixed collection gems lost their native item ID, rank or strength.");
        var paralysis = CollectopaediaCatalog.Rewards.First(reward => reward.ItemId == 3271);
        check(paralysis.FixedStrength == 0 && paralysis.Gem is { Minimum: 0, Maximum: 0, Chance: 8 },
            "A chance-only native gem reward lost its zero-strength range or activation chance.");
        var machina = CollectopaediaCatalog.Rewards.Single(reward => reward.Id == 109);
        check(machina.ItemId == 115 && machina.Equipment is not null && machina.Gem is null,
            "A legitimate collection weapon reward was excluded by the ordinary creation catalog.");
        foreach (bool future in new[] { false, true })
        {
            byte[] bytes = fixture(future);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, 7);
            bytes.AsSpan(0x1490a8, 48).Clear();
            int[] ids = future ? Enumerable.Range(319, 28).ToArray() : Enumerable.Range(1, 300).ToArray();
            var document = SaveDocument.Parse(bytes);
            check(document.CanInspectCollectopaedia && document.Collectopaedia.Select(entry => entry.Id).SequenceEqual(ids),
                "Collectopaedia mixed campaigns or exposed placeholder entries.");
            check(document.Collectopaedia.All(entry => !entry.IsRegistered), "Empty Collectopaedia flags were read as registered.");
            var fullPlan = document.PlanCollectopaediaCompletion(ids);
            check(fullPlan.NewEntries.Count == ids.Length && fullPlan.Rewards.Count == (future ? 10 : 123),
                "Full Collectopaedia planning misses page/category rewards.");
            check(fullPlan.Rewards.Select(reward => reward.Id).Order().SequenceEqual(
                future ? Enumerable.Range(124, 10) : Enumerable.Range(1, 123)), "Planning mixed campaign rewards.");
            check(document.PlanCollectopaediaCompletion([]).Rewards.Count == 0, "An empty selection awarded rewards.");
            foreach (var page in CollectopaediaCatalog.Pages.Where(page => page.Campaign == document.Campaign))
            {
                var pagePlan = document.PlanCollectopaediaCompletion(page.Entries.Select(entry => entry.Id));
                check(pagePlan.Rewards.Select(reward => reward.Id).SequenceEqual(
                    page.Categories.Select(category => category.Reward.Id).Append(page.Reward.Id)),
                    "Page rewards are duplicated, missing or out of native order.");
                var registeredCategory = page.Categories[0];
                var mixedBytes = (byte[])bytes.Clone();
                foreach (var entry in registeredCategory.Entries)
                {
                    int bit = 0x19e9 + entry.Id;
                    mixedBytes[0x148d6c + (bit >> 3)] |= (byte)(1 << (bit & 7));
                }
                var mixed = SaveDocument.Parse(mixedBytes);
                var mixedPlan = mixed.PlanCollectopaediaCompletion(page.Entries.Select(entry => entry.Id));
                check(mixedPlan.NewEntries.Count == page.Entries.Count - registeredCategory.Entries.Count
                    && mixedPlan.Rewards.Select(reward => reward.Id).SequenceEqual(
                        page.Categories.Skip(1).Select(category => category.Reward.Id).Append(page.Reward.Id)),
                    "Page completion awards an already-completed category again.");
                check(mixed.Serialize().AsSpan().SequenceEqual(mixedBytes), "Mixed-state planning changed the save.");
                foreach (var category in page.Categories)
                {
                    var categoryIds = category.Entries.Select(entry => entry.Id).ToArray();
                    check(document.PlanCollectopaediaCompletion(categoryIds).Rewards.Select(reward => reward.Id)
                        .SequenceEqual(new[] { category.Reward.Id }), "Partial pages incorrectly award page rewards.");
                    check(document.PlanCollectopaediaCompletion(categoryIds.SkipLast(1)).Rewards.Count == 0,
                        "An incomplete category incorrectly awards its reward.");
                    var partialBytes = (byte[])bytes.Clone();
                    foreach (int id in categoryIds.SkipLast(1))
                    {
                        int bit = 0x19e9 + id;
                        partialBytes[0x148d6c + (bit >> 3)] |= (byte)(1 << (bit & 7));
                    }
                    var partial = SaveDocument.Parse(partialBytes);
                    var finish = partial.PlanCollectopaediaCompletion([categoryIds[^1], categoryIds[^1]]);
                    check(finish.NewEntries.Count == 1 && finish.Rewards.Select(reward => reward.Id)
                        .SequenceEqual(new[] { category.Reward.Id }), "Finishing a category duplicates entries or misses its reward.");
                }
            }
            check(document.Serialize().AsSpan().SequenceEqual(bytes), "Completion planning changed the save.");
            foreach (int id in ids)
            {
                byte[] one = (byte[])bytes.Clone();
                int bit = 0x19e9 + id;
                int wordOffset = 0x148d6c + 4 * (bit >> 5);
                BinaryPrimitives.WriteUInt32LittleEndian(one.AsSpan(wordOffset), 1u << ((id + 9) & 31));
                var isolated = SaveDocument.Parse(one);
                check(isolated.Collectopaedia.Where(entry => entry.IsRegistered).Select(entry => entry.Id).SequenceEqual(new[] { id }),
                    $"Collectopaedia ID {id} reads an adjacent bit or word.");
                check(isolated.Serialize().AsSpan().SequenceEqual(one), "Reading Collectopaedia normalized or changed the save.");
            }
            foreach (int id in ids)
            {
                int bit = 0x19e9 + id;
                bytes[0x148d6c + (bit >> 3)] |= (byte)(1 << (bit & 7));
            }
            document = SaveDocument.Parse(bytes);
            check(document.Collectopaedia.All(entry => entry.IsRegistered), "Full Collectopaedia missed a completion bit.");
            var replay = document.PlanCollectopaediaCompletion(ids);
            check(replay.NewEntries.Count == 0 && replay.Rewards.Count == 0, "Already-completed collections award rewards again.");
            check(document.Collectopaedia.Select(entry => entry.MapId).Distinct().Count() == (future ? 2 : 21),
                "Collectopaedia page grouping differs from the native map IDs.");
            foreach (int id in new[] { -1, 0, 301, 318, 347, 349, int.MaxValue, future ? 1 : 319 })
            {
                reject(() => document.GetCollectopaediaEntry(id), "Collectopaedia exposed a placeholder or another campaign's item.");
                reject(() => document.PlanCollectopaediaCompletion([ids[0], id]), "Planning accepts an invalid campaign entry.");
            }
            check(document.Serialize().AsSpan().SequenceEqual(bytes), "Collectopaedia inspection changed inventory or flags.");
            foreach (uint version in new uint[] { 0, 6, 8, uint.MaxValue })
            {
                byte[] unknown = (byte[])bytes.Clone();
                BinaryPrimitives.WriteUInt32LittleEndian(unknown, version);
                var unsupported = SaveDocument.Parse(unknown);
                check(!unsupported.CanInspectCollectopaedia && unsupported.Collectopaedia.Count == 0,
                    "Collectopaedia interpreted an unverified save version.");
                reject(() => unsupported.GetCollectopaediaEntry(ids[0]), "Unverified layout exposed a registration flag.");
                reject(() => unsupported.PlanCollectopaediaCompletion(ids), "Planning accepts an unverified save format.");
                check(unsupported.Serialize().AsSpan().SequenceEqual(unknown), "Unverified Collectopaedia inspection changed bytes.");
            }
        }
        byte[] ambiguous = fixture(true);
        BinaryPrimitives.WriteUInt32LittleEndian(ambiguous, 7);
        ambiguous[0x152330] = 2;
        var unidentified = SaveDocument.Parse(ambiguous);
        check(!unidentified.CanInspectCollectopaedia && unidentified.Collectopaedia.Count == 0,
            "An unidentified campaign exposed a guessed Collectopaedia catalog.");
        reject(() => unidentified.PlanCollectopaediaCompletion([]), "Planning accepts an unidentified campaign.");
        check(unidentified.Serialize().AsSpan().SequenceEqual(ambiguous), "Unsupported planning changed the save.");
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var document = SaveDocument.Parse(bytes);
        if (!document.CanInspectCollectopaedia) return;
        foreach (var entry in document.Collectopaedia)
        {
            int bit = 0x19e9 + entry.Id;
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x148d6c + 4 * (bit >> 5)));
            check(entry.IsRegistered == ((word & (1u << ((entry.Id + 9) & 31))) != 0),
                "Real Collectopaedia differs from the native word and mask calculation.");
        }
        if (document.Collectopaedia.All(entry => entry.IsRegistered))
        {
            var replay = document.PlanCollectopaediaCompletion(document.Collectopaedia.Select(entry => entry.Id));
            check(replay.NewEntries.Count == 0 && replay.Rewards.Count == 0,
                "A completed real save receives duplicate collection rewards in the plan.");
        }
        check(document.Serialize().AsSpan().SequenceEqual(bytes), "Inspecting a real Collectopaedia changed the save.");
        Console.WriteLine($"Collectopaedia: {document.Collectopaedia.Count(entry => entry.IsRegistered)}/{document.Collectopaedia.Count} registered.");
    }
}
