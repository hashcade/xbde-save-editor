using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class GemTests
{
    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        check(GemCatalog.Definitions.Count == 528, "Gem catalog coverage changed.");
        check(GemCatalog.Find(1, 1) is { Minimum: 5, Maximum: 10, Chance: 0 }
            && GemCatalog.Find(1, 6) is { Minimum: 75, Maximum: 100 }, "Strength limits differ from game data.");
        check(GemCatalog.Find(98, 6) is null, "Cylinder impurity is offered as a gem effect.");
        foreach (bool future in new[] { false, true })
        {
            byte[] bytes = EquipmentTests.Fixture(fixture(future));
            var save = SaveDocument.Parse(bytes);
            var gem = save.GetGem(0);
            check(gem.CanEdit && gem.HasValidValue && gem.Label.Contains("100"), "Normal gem is not editable or labeled with strength.");
            gem.Set(gem.EffectId, gem.Rank, gem.Strength);
            check(save.Serialize().AsSpan().SequenceEqual(bytes), "An unchanged gem was normalized.");
            foreach (var rule in GemCatalog.Definitions)
            {
                foreach (int strength in new[] { rule.Minimum, rule.Maximum })
                {
                    var copy = SaveDocument.Parse(bytes);
                    var item = copy.GetGem(4);
                    item.Set(rule.EffectId, rule.Rank, strength);
                    check(item.Strength == strength && item.Chance == rule.Chance && item.HasValidValue,
                        $"Gem encoding failed: {rule.EffectId}/{rule.Rank}.");
                    check(item.CanFit(EquipmentSlot.Weapon) == rule.Allows(EquipmentSlot.Weapon)
                        && item.CanFit(EquipmentSlot.Head) == rule.Allows(EquipmentSlot.Head), "Gem fitting ignores attachment rules.");
                    CheckBoundary(bytes, copy.Serialize(), 4, check);
                    item.Maximize();
                    check(item.Strength == rule.Maximum && item.Chance == rule.Chance, "Gem maximum failed.");
                    var maximum = copy.Serialize();
                    item.Maximize();
                    check(copy.Serialize().AsSpan().SequenceEqual(maximum), "Gem maximum is not idempotent.");
                    reject(() => item.Set(rule.EffectId, rule.Rank, rule.Minimum - 1), "Below-minimum gem was accepted.");
                    reject(() => item.Set(rule.EffectId, rule.Rank, rule.Maximum + 1), "Above-maximum gem was accepted.");
                    check(copy.Serialize().AsSpan().SequenceEqual(maximum), "Rejected gem edit partially wrote bytes.");
                }
            }
            gem.Set(26, 6, 200);
            check(gem.Value == 6600 && gem.Label.Contains("200 / 25%"), "HP Steal packing or display differs.");
            gem.Set(38, 6, 25);
            check(gem.Value == 7705 && gem.Label.Contains("25% / 30%"), "Phys Def Down packing or display differs.");
            gem.Set(39, 6, 0);
            check(gem.Value == 6400 && gem.Label.EndsWith("25%"), "Chance-only gem encoding differs.");
            var beforeIncompatible = save.Serialize();
            reject(() => gem.Set(24, 6, 200), "Armor-only effect accepted on a weapon.");
            check(save.Serialize().AsSpan().SequenceEqual(beforeIncompatible), "Incompatible effect rejection changed bytes.");
            save.GetGem(4).Set(24, 6, 200);
            reject(() => save.GetCharacter(1).GetEquipment(EquipmentSlot.Weapon).SetGems([4, null, null]),
                "Armor-only gem was fitted to a weapon.");
            reject(() => save.GetGem(2).Set(24, 6, 200), "Unequipped weapon's fitted gem ignored compatibility.");
            var fixedBytes = (byte[])bytes.Clone();
            BinaryPrimitives.WriteUInt16LittleEndian(fixedBytes.AsSpan(EquipmentTests.Gem + 4 * 0x2c + 4), 1337);
            var fixedCopy = SaveDocument.Parse(fixedBytes);
            var fixedGem = fixedCopy.GetGem(4);
            fixedGem.Set(1, 6, 99);
            check(BinaryPrimitives.ReadUInt16LittleEndian(fixedCopy.Serialize().AsSpan(EquipmentTests.Gem + 4 * 0x2c + 4)) == 1337,
                "Strength-only edit cleared the fixed item ID.");
            fixedGem.Set(26, 5, 100);
            var converted = fixedCopy.Serialize();
            int convertedOffset = EquipmentTests.Gem + 4 * 0x2c;
            check(BinaryPrimitives.ReadUInt16LittleEndian(converted.AsSpan(convertedOffset + 4)) == 0
                && converted[convertedOffset + 0x17] == 5 && fixedGem.Chance == 20,
                "Effect/rank conversion retained unrelated fixed metadata.");
            CheckBoundary(fixedBytes, converted, 4, check);
            reject(() => save.GetGem(5).Maximize(), "Cylinder was edited.");
            reject(() => save.GetGem(499).Maximize(), "Missing gem was edited.");
            foreach (var invalid in new[] { (98, 6), (0, 6), (1, 0), (1, 7) })
                reject(() => gem.Set(invalid.Item1, invalid.Item2, 10), "Invalid effect/rank accepted.");
            var malformed = (byte[])bytes.Clone();
            BinaryPrimitives.WriteUInt16LittleEndian(malformed.AsSpan(EquipmentTests.Gem + 2), 2);
            reject(() => SaveDocument.Parse(malformed).GetGem(0).Maximize(), "Wrong gem type was edited.");
            malformed = (byte[])bytes.Clone();
            BinaryPrimitives.WriteUInt16LittleEndian(malformed.AsSpan(EquipmentTests.Gem + 0x1a), 2);
            reject(() => SaveDocument.Parse(malformed).GetGem(0).Maximize(), "Multi-effect crystal was edited.");
        }
        byte[] unknown = EquipmentTests.Fixture(fixture(false));
        unknown[0x152330] = 1;
        reject(() => SaveDocument.Parse(unknown).GetGem(0).Maximize(), "Unknown campaign allowed gem editing.");
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var save = SaveDocument.Parse(bytes);
        foreach (var gem in save.Gems.Where(gem => gem.CanEdit))
        {
            var copy = SaveDocument.Parse(bytes);
            var item = copy.GetGem(gem.Index);
            if (item.HasValidValue)
            {
                item.Set(item.EffectId, item.Rank, item.Strength);
                check(copy.Serialize().AsSpan().SequenceEqual(bytes), "Unchanged real gem changed bytes.");
            }
            item.Maximize();
            check(item.HasValidValue, "Real gem maximum is invalid.");
            CheckBoundary(bytes, copy.Serialize(), gem.Index, check);
            item.Set(1, 6, 100);
            check(item.Value == 100, "Real gem conversion failed.");
            CheckBoundary(bytes, copy.Serialize(), gem.Index, check);
        }
        check(save.Serialize().AsSpan().SequenceEqual(bytes), "Real gem inspection changed bytes.");
    }

    private static void CheckBoundary(byte[] original, byte[] edited, int index, Action<bool, string> check)
    {
        int offset = EquipmentTests.Gem + index * 0x2c;
        check(original.AsSpan(0, offset + 4).SequenceEqual(edited.AsSpan(0, offset + 4))
            && original.AsSpan(offset + 6, 0x10).SequenceEqual(edited.AsSpan(offset + 6, 0x10))
            && original.AsSpan(offset + 0x18, 4).SequenceEqual(edited.AsSpan(offset + 0x18, 4))
            && original.AsSpan(offset + 0x20).SequenceEqual(edited.AsSpan(offset + 0x20)),
            "Gem edit changed metadata, equipment references or unrelated bytes.");
    }
}
