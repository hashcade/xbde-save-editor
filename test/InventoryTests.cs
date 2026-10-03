using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class InventoryTests
{
    internal static byte[] Fixture(byte[] data)
    {
        foreach (int start in new[] { 0x31970, 0x34080, 0x36790, 0x38ea0 }) data.AsSpan(start, 500 * 20).Clear();
        data.AsSpan(0x46900, 14 * 4).Clear();
        return data;
    }

    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        check(InventoryCatalog.Definitions.Count == 1353, "Inventory catalog coverage changed.");
        foreach (bool future in new[] { false, true })
        {
            byte[] bytes = Fixture(EquipmentTests.Fixture(fixture(future)));
            foreach (var kind in new[] { InventoryKind.Collectables, InventoryKind.Materials, InventoryKind.ArtManuals })
            {
                var save = SaveDocument.Parse(bytes);
                var definition = InventoryCatalog.Definitions.First(item => item.Kind == kind);
                var item = save.AddInventoryItem(definition.Id, 1);
                check(item.CanEdit && item.Index == 0 && item.Quantity == 1, "Added inventory item has an invalid header.");
                int offset = Offset(kind);
                int counter = 0x46900 + (int)kind * 4;
                Boundary(bytes, save.Serialize(), offset, 20, counter, check);
                check(BinaryPrimitives.ReadUInt32LittleEndian(save.Serialize().AsSpan(counter)) == 1,
                    "Creation did not update the game's inventory serial counter.");
                save.AddInventoryItem(definition.Id, 98);
                check(save.Inventory(kind).Count == 1 && item.Quantity == 99, "Adding to a stack created a duplicate.");
                byte[] beforeReject = save.Serialize();
                reject(() => save.AddInventoryItem(definition.Id, 1), "Overfull stack accepted.");
                reject(() => item.SetQuantity(0), "Zero quantity accepted without deletion.");
                reject(() => item.SetQuantity(100), "Over-limit quantity accepted.");
                check(save.Serialize().AsSpan().SequenceEqual(beforeReject), "Rejected stack edit mutated bytes.");
                item.SetFavorite(true);
                check(item.Favorite, "Favorite flag failed.");
                item.SetQuantity(7);
                save.MaxInventoryQuantities(kind);
                check(item.Quantity == 99, "Bulk quantities missed a stack.");
                var maximum = save.Serialize();
                save.MaxInventoryQuantities(kind);
                check(save.Serialize().AsSpan().SequenceEqual(maximum), "Bulk maximum is not idempotent.");
                item.Delete();
                check(save.Inventory(kind).Count == 0, "Deleted item remains visible.");
                var next = save.AddInventoryItem(definition.Id, 2);
                check(next.Index == 0 && next.Quantity == 2 && !next.Favorite, "Deleted slot was not properly reused.");
                check(BinaryPrimitives.ReadUInt32LittleEndian(save.Serialize().AsSpan(counter)) == 2,
                    "Serial counter was rewound on reuse.");
                byte[] malformed = (byte[])bytes.Clone();
                malformed[offset + 0x10] = 2;
                var protectedSave = SaveDocument.Parse(malformed);
                check(protectedSave.AddInventoryItem(definition.Id, 1).Index == 1, "Unrecognized occupancy flag was overwritten.");
                var overflow = (byte[])bytes.Clone();
                BinaryPrimitives.WriteUInt32LittleEndian(overflow.AsSpan(counter), uint.MaxValue);
                var overflowSave = SaveDocument.Parse(overflow);
                reject(() => overflowSave.AddInventoryItem(definition.Id, 1), "Overflowed serial counter was accepted.");
                check(overflowSave.Serialize().AsSpan().SequenceEqual(overflow), "Overflow rejection changed bytes.");
                var full = (byte[])bytes.Clone();
                for (int index = 0; index < 500; index++) full[offset + index * 20 + 16] = 1;
                var fullSave = SaveDocument.Parse(full);
                reject(() => fullSave.AddInventoryItem(definition.Id, 1), "Full inventory was overwritten.");
                check(fullSave.Serialize().AsSpan().SequenceEqual(full), "Full inventory rejection changed bytes.");
                var duplicate = save.Serialize();
                duplicate.AsSpan(offset, 20).CopyTo(duplicate.AsSpan(offset + 20, 20));
                BinaryPrimitives.WriteUInt16LittleEndian(duplicate.AsSpan(offset + 20), 1);
                var duplicateSave = SaveDocument.Parse(duplicate);
                reject(() => duplicateSave.AddInventoryItem(definition.Id, 1), "Duplicate stacks accepted another item.");
                check(duplicateSave.Serialize().AsSpan().SequenceEqual(duplicate), "Duplicate rejection changed bytes.");
                BinaryPrimitives.WriteUInt16LittleEndian(duplicate.AsSpan(offset + 20 + 2), 0);
                var invalidBank = SaveDocument.Parse(duplicate);
                reject(() => invalidBank.MaxInventoryQuantities(kind), "Malformed record accepted bulk editing.");
                check(invalidBank.Serialize().AsSpan().SequenceEqual(duplicate), "Bulk rejection partially changed stacks.");
                var staleCounter = save.Serialize();
                BinaryPrimitives.WriteUInt32LittleEndian(staleCounter.AsSpan(counter), 0);
                BinaryPrimitives.WriteUInt32LittleEndian(staleCounter.AsSpan(offset + 12), 50);
                var repaired = SaveDocument.Parse(staleCounter);
                var other = InventoryCatalog.Definitions.First(item => item.Kind == kind && item.Id != definition.Id);
                repaired.AddInventoryItem(other.Id, 1);
                check(BinaryPrimitives.ReadUInt32LittleEndian(repaired.Serialize().AsSpan(counter)) == 51,
                    "New item reused an active serial after a stale counter.");
            }
            var gems = SaveDocument.Parse(bytes);
            var gem = gems.AddGem(26, 6, 200);
            check(gem.Index == 6 && gem.CanEdit && gem.Value == 6600 && gem.CanDelete,
                "Crafted gem was not initialized or allocated safely.");
            Boundary(bytes, gems.Serialize(), 0x2c380 + 6 * 44, 44, 0x4690c, check);
            gem.Delete();
            check(!gem.Exists, "Deleted gem still exists.");
            reject(() => gems.GetGem(0).Delete(), "Equipped gem was deleted.");
            reject(() => gems.GetGem(2).Delete(), "Gem in unequipped equipment was deleted.");
            reject(() => gems.GetGem(5).Delete(), "Cylinder was deleted through gem editor.");
            var unchanged = gems.Serialize();
            reject(() => gems.AddGem(98, 6, 10), "Unused gem effect accepted.");
            reject(() => gems.AddGem(1, 6, 101), "Over-limit new gem accepted.");
            check(gems.Serialize().AsSpan().SequenceEqual(unchanged), "Rejected gem creation changed bytes.");
            var dangling = (byte[])bytes.Clone();
            BinaryPrimitives.WriteUInt32LittleEndian(dangling.AsSpan(0x3b10 + 2 * 48 + 0x18), 6 | 3u << 16);
            dangling[0x3b10 + 2 * 48 + 0x10] = 2;
            check(SaveDocument.Parse(dangling).AddGem(1, 6, 100).Index == 7,
                "Creation replaced a dangling gem reference in unrecognized equipment.");
            var fullGems = (byte[])bytes.Clone();
            for (int index = 0; index < 500; index++) fullGems[0x2c380 + index * 44 + 16] = 1;
            var fullGemSave = SaveDocument.Parse(fullGems);
            reject(() => fullGemSave.AddGem(1, 6, 100), "Full gem inventory was overwritten.");
            check(fullGemSave.Serialize().AsSpan().SequenceEqual(fullGems), "Full gem inventory rejection changed bytes.");
            reject(() => gems.AddInventoryItem(InventoryCatalog.Definitions.First(item => item.Kind == InventoryKind.KeyItems).Id, 1),
                "Quest item creation was offered as ordinary inventory editing.");
            reject(() => gems.MaxInventoryQuantities(InventoryKind.KeyItems), "Quest item bulk editing accepted.");
        }
        byte[] ambiguous = Fixture(EquipmentTests.Fixture(fixture(false)));
        ambiguous[0x152330] = 1;
        var unknown = SaveDocument.Parse(ambiguous);
        reject(() => unknown.AddGem(1, 6, 100), "Unknown campaign allowed gem creation.");
        reject(() => unknown.AddInventoryItem(1852, 1), "Unknown campaign allowed item creation.");
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var save = SaveDocument.Parse(bytes);
        foreach (var kind in new[] { InventoryKind.Collectables, InventoryKind.Materials, InventoryKind.ArtManuals })
        {
            foreach (var item in save.Inventory(kind).Where(item => item.CanEdit))
            {
                var copy = SaveDocument.Parse(bytes);
                copy.GetInventoryItem(kind, item.Index).SetQuantity(item.Quantity == 99 ? 98 : 99);
                Boundary(bytes, copy.Serialize(), Offset(kind) + item.Index * 20 + 8, 2, -10, check);
            }
            var absent = InventoryCatalog.Definitions.FirstOrDefault(definition => definition.Kind == kind
                && save.Inventory(kind).All(item => item.ItemId != definition.Id));
            if (absent is not null && save.Inventory(kind).Count < 500)
            {
                var copy = SaveDocument.Parse(bytes);
                var added = copy.AddInventoryItem(absent.Id, 1);
                Boundary(bytes, copy.Serialize(), Offset(kind) + added.Index * 20, 20, 0x46900 + (int)kind * 4, check);
            }
        }
        var gemCopy = SaveDocument.Parse(bytes);
        var gem = gemCopy.AddGem(1, 6, 100);
        Boundary(bytes, gemCopy.Serialize(), 0x2c380 + gem.Index * 44, 44, 0x4690c, check);
    }

    private static int Offset(InventoryKind kind) => kind switch
    {
        InventoryKind.Collectables => 0x31970, InventoryKind.Materials => 0x34080,
        InventoryKind.KeyItems => 0x36790, InventoryKind.ArtManuals => 0x38ea0,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static void Boundary(byte[] before, byte[] after, int start, int length, int counter, Action<bool, string> check)
    {
        for (int offset = 0; offset < before.Length; offset++)
            if (before[offset] != after[offset] && !(offset >= start && offset < start + length)
                && !(offset >= counter && offset < counter + 4))
                throw new InvalidOperationException($"Inventory edit changed unrelated byte {offset:X}.");
        check(true, "Inventory byte boundary failed.");
    }
}
