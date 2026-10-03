using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class CollectopaediaCompletionTests
{
    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        foreach (bool future in new[] { false, true })
        {
            byte[] bytes = EquipmentInventoryTests.Fixture(fixture(future));
            bytes.AsSpan(0x2c380, 500 * 0x2c).Clear();
            bytes.AsSpan(0x1490a8, 48).Clear();
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x31972), 10);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x31974), 1852);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x31976), 10);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x31978), 37);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x3197c), 99);
            bytes[0x31980] = 1;
            bytes[0x31981] = 1;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x152368 + 0xf4), 12345);
            foreach (int id in new[] { 132, 133, 134 })
            {
                int bit = 0x2838 + id;
                bytes[0x50 + bit / 8] &= (byte)~(1 << (bit % 8));
            }
            var save = SaveDocument.Parse(bytes);
            int[] ids = save.Collectopaedia.Select(entry => entry.Id).ToArray();
            var plan = save.PlanCollectopaediaCompletion(ids);
            check(plan.NewAchievementIds.SequenceEqual(future ? [] : new[] { 132, 133, 134 }),
                "Collection completion plans unrelated or other-campaign achievements.");
            var heldRecord = save.GetCollectopaediaEntry(ids[0]);
            save.CompleteCollectopaedia(ids);
            byte[] actual = save.Serialize();
            byte[] expected = (byte[])bytes.Clone();
            foreach (int id in ids)
            {
                int bit = 0x19e9 + id;
                expected[0x148d6c + bit / 8] |= (byte)(1 << (bit % 8));
            }
            foreach (int id in plan.NewAchievementIds)
            {
                int bit = 0x2838 + id;
                expected[0x50 + bit / 8] |= (byte)(1 << (bit % 8));
            }
            foreach (var group in plan.Rewards.Where(reward => reward.Equipment is not null)
                .GroupBy(reward => reward.Equipment!.Slot))
            {
                int index = 0;
                foreach (var reward in group)
                {
                    EquipmentInventoryTests.Initialize(expected, reward.Equipment!, index, (uint)index + 1);
                    check(save.GetInventoryEquipment(group.Key, index).IsValid,
                        $"Collection equipment reward {reward.Id} is not a valid native record.");
                    index++;
                }
            }
            int gemIndex = 0;
            foreach (var reward in plan.Rewards.Where(reward => reward.Gem is not null))
            {
                var gem = save.GetGem(gemIndex);
                var rule = reward.Gem!;
                check(gem.CanEquip && gem.HasValidValue && gem.EffectId == reward.EffectId && gem.Rank == reward.Rank
                    && (reward.FixedStrength == 0 || gem.Strength == reward.FixedStrength),
                    $"Collection gem reward {reward.Id} lost its native effect/rank/value.");
                InitializeGem(expected, reward, gemIndex, (uint)gemIndex + 1, gem.Strength);
                gemIndex++;
            }
            check(heldRecord.IsRegistered && actual.AsSpan().SequenceEqual(expected),
                "Collection completion changed characters, collectable stocks, existing records or unrelated bytes.");
            var replay = save.CompleteCollectopaedia(ids);
            check(replay.NewEntries.Count == 0 && replay.Rewards.Count == 0 && replay.NewAchievementIds.Count == 0
                && save.Serialize().AsSpan().SequenceEqual(actual), "Collection completion is not idempotent.");
            foreach (int invalid in new[] { 0, 301, 347, future ? 1 : 319 })
            {
                var rejected = SaveDocument.Parse(bytes);
                reject(() => rejected.CompleteCollectopaedia([ids[0], invalid]), "Invalid entries were partially registered.");
                check(rejected.Serialize().AsSpan().SequenceEqual(bytes), "Rejected collection entries changed the save.");
            }
            var allTypes = plan.Rewards.Select(reward => reward.ItemType).Distinct().ToArray();
            foreach (int type in allTypes)
            {
                int start = type switch
                {
                    2 => 0x3b10, 3 => 0x2c380, 4 => 0x98d0, 5 => 0xf690,
                    6 => 0x15450, 7 => 0x1b210, 8 => 0x20fd0,
                    _ => throw new InvalidOperationException("Unexpected reward bank.")
                };
                int size = type == 3 ? 0x2c : 0x30;
                byte[] full = (byte[])bytes.Clone();
                for (int index = 0; index < 500; index++) full[start + index * size + 0x10] = 2;
                var rejected = SaveDocument.Parse(full);
                reject(() => rejected.CompleteCollectopaedia(ids), "A full bank accepted collection rewards.");
                check(rejected.Serialize().AsSpan().SequenceEqual(full), "A failed collection batch partially changed another bank or flags.");
                foreach (uint serial in new[] { uint.MaxValue, uint.MaxValue - 1 })
                {
                    byte[] overflow = (byte[])bytes.Clone();
                    BinaryPrimitives.WriteUInt32LittleEndian(overflow.AsSpan(0x46900 + type * 4), serial);
                    rejected = SaveDocument.Parse(overflow);
                    reject(() => rejected.CompleteCollectopaedia(ids), "Collection reward serial overflow was accepted.");
                    check(rejected.Serialize().AsSpan().SequenceEqual(overflow), "Serial overflow left partial collection changes.");
                }
            }
            var single = SaveDocument.Parse(bytes);
            reject(() => single.CompleteCollectopaedia(null!), "A null collection selection was accepted.");
            check(single.Serialize().AsSpan().SequenceEqual(bytes), "Null selection changed collection state.");
            var first = single.CompleteCollectopaedia([ids[0], ids[0]]);
            check(first.NewEntries.Count == 1 && first.Rewards.Count == 0
                && single.Collectopaedia.Count(entry => entry.IsRegistered) == 1
                && first.NewAchievementIds.SequenceEqual(future ? [] : new[] { 132 }),
                "Single-entry collection editing awards incomplete pages or duplicates input entries.");
            foreach (uint version in new uint[] { 0, 6, 8 })
            {
                byte[] unsupported = (byte[])bytes.Clone();
                BinaryPrimitives.WriteUInt32LittleEndian(unsupported, version);
                var rejected = SaveDocument.Parse(unsupported);
                reject(() => rejected.CompleteCollectopaedia(ids), "Unverified format accepted collection completion.");
                check(rejected.Serialize().AsSpan().SequenceEqual(unsupported), "Unverified collection completion changed bytes.");
            }
            byte[] ambiguous = (byte[])bytes.Clone();
            ambiguous.AsSpan(0x152318, 25).Clear();
            BinaryPrimitives.WriteUInt16LittleEndian(ambiguous.AsSpan(0x152318), 1);
            ambiguous[0x152330] = 1;
            var unidentified = SaveDocument.Parse(ambiguous);
            reject(() => unidentified.CompleteCollectopaedia([]), "Unknown campaign accepts completion.");
            check(unidentified.Serialize().AsSpan().SequenceEqual(ambiguous), "Unknown-campaign completion changed bytes.");
        }
    }

    private static void InitializeGem(byte[] bytes, CollectopaediaRewardDefinition reward, int index, uint serial, int strength)
    {
        int start = 0x2c380 + index * 0x2c;
        bytes.AsSpan(start, 0x2c).Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start), (ushort)index);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start + 2), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start + 4), (ushort)reward.ItemId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start + 6), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start + 8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(start + 0xc), serial);
        bytes[start + 0x10] = 1;
        bytes[start + 0x16] = (byte)reward.Rank;
        bytes[start + 0x17] = (byte)reward.Gem!.Attribute;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start + 0x1a), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start + 0x1c), (ushort)reward.EffectId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start + 0x1e), (ushort)(strength | reward.Gem.Chance << 8));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x4690c), serial);
    }
}
