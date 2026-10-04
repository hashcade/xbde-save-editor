using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class AchievementTests
{
    public static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] Fresh(bool future = false)
        {
            byte[] bytes = fixture(future);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, 7);
            bytes.AsSpan(0x557, 25).Clear();
            bytes.AsSpan(0xe30, 400).Clear();
            return bytes;
        }
        check(AchievementCatalog.All.Count == 200, "Achievement catalog is incomplete.");
        check(AchievementCatalog.All.Count(item => item.Category == AchievementCategory.Trials) == 150
            && AchievementCatalog.All.Count(item => item.Category == AchievementCategory.Records) == 50,
            "Achievement category mapping differs.");
        foreach (var definition in AchievementCatalog.All)
        {
            byte[] original = Fresh();
            var save = SaveDocument.Parse(original);
            var item = save.GetAchievement(definition.Id);
            check(!item.Completed && item.CanUnlock, "Fresh achievement is not unlockable.");
            item.Unlock();
            int bit = 0x2838 + definition.Id % 200;
            int flagOffset = 0x50 + (bit >> 3);
            int counterOffset = 0xe30 + 2 * (definition.Id % 200);
            byte[] expected = (byte[])original.Clone();
            expected[flagOffset] |= (byte)(1 << (bit & 7));
            if (definition.HasCounter)
                BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(counterOffset), (ushort)definition.Required);
            check(save.Serialize().AsSpan().SequenceEqual(expected), $"Achievement {item.Id} changed unrelated bytes.");
            check(item.Completed && !item.CanUnlock && save.Achievements.Count(other => other.Completed) == 1,
                "Single unlock completed a different achievement.");
            check(item.Progress == (definition.HasCounter ? definition.Required : null), "Achievement progress differs.");
            item.Unlock();
            check(save.Serialize().AsSpan().SequenceEqual(expected), "Achievement unlock is not idempotent.");
        }
        var high = Fresh();
        BinaryPrimitives.WriteUInt16LittleEndian(high.AsSpan(0xe30 + 4 * 2), 65000);
        var highSave = SaveDocument.Parse(high);
        highSave.GetAchievement(4).Unlock();
        check(highSave.GetAchievement(4).Progress == 65000, "Unlock lowered existing progress.");
        byte[] bytes = Fresh();
        var all = SaveDocument.Parse(bytes);
        all.UnlockAllAchievements();
        check(all.Achievements.All(item => item.Completed), "Bulk unlock missed achievements.");
        check(all.GetAchievement(200).Completed && all.Serialize()[0x557] == 255,
            "Achievement 200 did not wrap into the first flag bit.");
        byte[] bulk = all.Serialize();
        for (int offset = 0; offset < bulk.Length; offset++)
            if (offset is not (>= 0x557 and < 0x570) and not (>= 0xe30 and < 0xfc0))
                if (bulk[offset] != bytes[offset]) throw new InvalidOperationException($"Bulk unlock altered {offset:X}.");
        all.UnlockAllAchievements();
        check(all.Serialize().AsSpan().SequenceEqual(bulk), "Bulk unlock is not idempotent.");
        reject(() => all.GetAchievement(0), "Achievement zero was accepted.");
        reject(() => all.GetAchievement(201), "Invalid achievement ID was accepted.");
        byte[] unknown = Fresh();
        unknown.AsSpan(0x152318, 25).Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(unknown.AsSpan(0x152318), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(unknown.AsSpan(0x15231a), 7);
        unknown[0x152330] = 2;
        foreach (byte[] unsupported in new[] { Fresh(true), fixture(false), unknown })
        {
            var save = SaveDocument.Parse(unsupported);
            check(!save.CanEditAchievements, "Unverified achievement layout was editable.");
            reject(() => save.GetAchievement(4).Unlock(), "Unsupported single unlock succeeded.");
            reject(save.UnlockAllAchievements, "Unsupported bulk unlock succeeded.");
            check(!save.GetAchievement(4).CanRepairCounter, "Unsupported counter repair is enabled.");
            reject(() => save.GetAchievement(4).RepairCounter(), "Unsupported single counter repair succeeded.");
            reject(save.RepairAllAchievementCounters, "Unsupported bulk counter repair succeeded.");
            check(save.Serialize().AsSpan().SequenceEqual(unsupported), "Rejected unlock changed the save.");
        }
        byte[] completed = Fresh();
        completed.AsSpan(0x557, 25).Fill(255);
        var completedSave = SaveDocument.Parse(completed);
        check(completedSave.GetAchievement(7).Completed && completedSave.GetAchievement(7).HasUnmetCompletedCounter
            && completedSave.GetAchievement(7).CounterMeetsRequirement == false,
            "A completion bit concealed an unmet counter.");
        check(completedSave.GetAchievement(1).CounterMeetsRequirement is null
            && !completedSave.GetAchievement(1).HasUnmetCompletedCounter,
            "An event achievement was assigned an unverified counter.");
        BinaryPrimitives.WriteUInt16LittleEndian(completed.AsSpan(0xe3e), 4256);
        completedSave = SaveDocument.Parse(completed);
        check(completedSave.GetAchievement(7).Progress == 4256 && completedSave.GetAchievement(7).HasUnmetCompletedCounter,
            "The reported below-threshold completed record was misclassified.");
        completedSave.UnlockAllAchievements();
        check(completedSave.Serialize().AsSpan().SequenceEqual(completed), "Completed records were silently normalized.");
        foreach (var definition in AchievementCatalog.All)
            foreach (bool flagged in new[] { false, true })
                foreach (int progress in definition.HasCounter
                    ? new[] { Math.Max(0, definition.Required - 1), definition.Required, Math.Min(ushort.MaxValue, definition.Required + 1), ushort.MaxValue }.Distinct()
                    : new[] { 65_000 })
                {
                    byte[] original = Fresh();
                    int bit = 0x2838 + definition.Id % 200;
                    if (flagged) original[0x50 + (bit >> 3)] |= (byte)(1 << (bit & 7));
                    int offset = 0xe30 + 2 * (definition.Id % 200);
                    BinaryPrimitives.WriteUInt16LittleEndian(original.AsSpan(offset), (ushort)progress);
                    var save = SaveDocument.Parse(original);
                    var item = save.GetAchievement(definition.Id);
                    bool needsRepair = flagged && definition.HasCounter
                        && (definition.ConditionType == 3 ? progress != definition.Required : progress < definition.Required);
                    check(item.CanRepairCounter == needsRepair, "Counter repair availability differs from the flag and native condition.");
                    byte[] expected = (byte[])original.Clone();
                    if (needsRepair) BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(offset), (ushort)definition.Required);
                    item.RepairCounter();
                    check(save.Serialize().AsSpan().SequenceEqual(expected), "Counter repair changed flags, rewards, events or unrelated fields.");
                    check(!item.CanRepairCounter && item.Completed == flagged, "Counter repair unlocked an incomplete achievement or left an unmet condition.");
                    item.RepairCounter();
                    check(save.Serialize().AsSpan().SequenceEqual(expected), "Counter repair is not idempotent.");
                }
        VerifyRealSave(completed, check);
        VerifyRealSave(Fresh(), check);
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var save = SaveDocument.Parse(bytes);
        if (!save.CanEditAchievements) return;
        byte[] expected = (byte[])bytes.Clone();
        foreach (var definition in AchievementCatalog.All.Where(item => item.HasCounter))
        {
            int bit = 0x2838 + definition.Id % 200;
            if ((bytes[0x50 + (bit >> 3)] & (1 << (bit & 7))) == 0) continue;
            int offset = 0xe30 + 2 * (definition.Id % 200);
            int progress = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
            bool unmet = definition.ConditionType == 3 ? progress != definition.Required : progress < definition.Required;
            if (unmet) BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(offset), (ushort)definition.Required);
        }
        save.RepairAllAchievementCounters();
        check(save.Serialize().AsSpan().SequenceEqual(expected), "Bulk counter repair changed more than unmet completed counters.");
        check(save.Achievements.All(item => !item.HasUnmetCompletedCounter), "Bulk counter repair missed a flagged record.");
        save.RepairAllAchievementCounters();
        check(save.Serialize().AsSpan().SequenceEqual(expected), "Bulk counter repair is not idempotent.");
    }
}
