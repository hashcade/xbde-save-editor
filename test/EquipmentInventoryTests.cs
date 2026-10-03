using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class EquipmentInventoryTests
{
    internal static byte[] Fixture(byte[] bytes)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 7);
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
            bytes.AsSpan(Start(slot), 500 * 0x30).Clear();
        bytes.AsSpan(0x46900 + 2 * 4, 7 * 4).Clear();
        for (int id = 1; id <= 15; id++)
            foreach (var slot in Enum.GetValues<EquipmentSlot>())
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(Reference(id, slot)), 0xffff);
        return bytes;
    }

    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        check(EquipmentDefinitions.All.Count == 1572, "Equipment construction metadata is incomplete.");
        foreach (bool future in new[] { false, true })
        {
            byte[] bytes = Fixture(fixture(future));
            var original = SaveDocument.Parse(bytes);
            var definitions = Enum.GetValues<EquipmentSlot>().SelectMany(original.CreatableEquipment).ToArray();
            check(definitions.Length > 100 && definitions.All(item => item.IsOrdinary), "Creation includes dummy or protected equipment.");
            foreach (var definition in definitions)
            {
                var save = SaveDocument.Parse(bytes);
                var item = save.AddEquipment(definition.Id);
                byte[] expected = (byte[])bytes.Clone();
                Initialize(expected, definition, 0, 1);
                check(item.Index == 0 && item.IsValid && item.CanDelete
                    && save.Serialize().AsSpan().SequenceEqual(expected), $"Equipment {definition.Id} ({definition.Slot}) differs from the native initializer or changes unrelated bytes.");
                item.SetFavorite(true);
                expected[Start(definition.Slot) + 0x11] = 1;
                check(save.Serialize().AsSpan().SequenceEqual(expected), "Equipment favorite changed unrelated bytes.");
                item.Delete();
                expected[Start(definition.Slot) + 0x10] = 0;
                check(!item.Exists && save.Serialize().AsSpan().SequenceEqual(expected), "Deleting equipment shifted records or erased its gems.");
            }
            foreach (var slot in Enum.GetValues<EquipmentSlot>())
            {
                var save = SaveDocument.Parse(bytes);
                var definition = save.CreatableEquipment(slot).First();
                var first = save.AddEquipment(definition.Id);
                var second = save.AddEquipment(definition.Id);
                check(first.Index == 0 && second.Index == 1 && save.InventoryEquipment(slot).Count == 2,
                    "Distinct equipment was incorrectly merged into a stack.");
                byte[] references = save.Serialize();
                BinaryPrimitives.WriteUInt32LittleEndian(references.AsSpan(Reference(1, slot)), (uint)first.Index | ((uint)Type(slot) << 16));
                var equipped = SaveDocument.Parse(references);
                check(!equipped.GetInventoryEquipment(slot, first.Index).CanDelete, "Equipped equipment can be deleted.");
                reject(() => equipped.GetInventoryEquipment(slot, first.Index).Delete(), "Equipped equipment was deleted.");
                check(equipped.Serialize().AsSpan().SequenceEqual(references), "Rejected equipment deletion changed bytes.");
                byte[] inactive = save.Serialize();
                BinaryPrimitives.WriteUInt32LittleEndian(inactive.AsSpan(Reference(10, slot)), (uint)Type(slot) << 16);
                var inactiveSave = SaveDocument.Parse(inactive);
                check(!inactiveSave.GetInventoryEquipment(slot, 0).CanDelete, "Inactive character reference is deletable.");
                reject(() => inactiveSave.GetInventoryEquipment(slot, 0).Delete(), "Inactive character's equipment was deleted.");
                check(inactiveSave.Serialize().AsSpan().SequenceEqual(inactive), "Inactive reference rejection changed bytes.");
                foreach (int id in new[] { 1, 10 })
                {
                    byte[] dangling = (byte[])bytes.Clone();
                    BinaryPrimitives.WriteUInt32LittleEndian(dangling.AsSpan(Reference(id, slot)), (uint)Type(slot) << 16);
                    var reserved = SaveDocument.Parse(dangling);
                    check(reserved.AddEquipment(definition.Id).Index == 1, "Creation overwrote a joined or inactive character's dangling reference.");
                }
                byte[] occupied = (byte[])bytes.Clone();
                occupied[Start(slot) + 0x10] = 2;
                check(SaveDocument.Parse(occupied).AddEquipment(definition.Id).Index == 1, "An unrecognized presence flag was reused.");
                byte[] full = (byte[])bytes.Clone();
                for (int index = 0; index < 500; index++) full[Start(slot) + index * 0x30 + 0x10] = 1;
                var protectedSave = SaveDocument.Parse(full);
                reject(() => protectedSave.AddEquipment(definition.Id), "Full equipment bank accepted creation.");
                check(protectedSave.Serialize().AsSpan().SequenceEqual(full), "Full equipment rejection partially wrote bytes.");
            }
            var bulk = SaveDocument.Parse(bytes);
            int character = future ? 14 : 2;
            var targets = bulk.CreatableEquipment(EquipmentSlot.Weapon).Where(item => item.Characters.Contains(character)).ToArray();
            check(targets.Length > 5, "Bulk test character has no useful equipment catalog.");
            bulk.AddEquipment(targets[0].Id);
            var created = bulk.FillMissingEquipment(EquipmentSlot.Weapon, character);
            byte[] bulkExpected = (byte[])bytes.Clone();
            for (int index = 0; index < targets.Length; index++) Initialize(bulkExpected, targets[index], index, (uint)index + 1);
            check(created.Count == targets.Length - 1 && bulk.Serialize().AsSpan().SequenceEqual(bulkExpected), "Bulk creation does not preserve existing equipment or native records.");
            check(bulk.FillMissingEquipment(EquipmentSlot.Weapon, character).Count == 0
                && bulk.Serialize().AsSpan().SequenceEqual(bulkExpected), "Bulk equipment filling is not idempotent.");
            byte[] shortage = (byte[])bytes.Clone();
            for (int index = 0; index < 499; index++) shortage[Start(EquipmentSlot.Weapon) + index * 0x30 + 0x10] = 2;
            var tooFull = SaveDocument.Parse(shortage);
            reject(() => tooFull.FillMissingEquipment(EquipmentSlot.Weapon, character), "Bulk creation did not preflight capacity.");
            check(tooFull.Serialize().AsSpan().SequenceEqual(shortage), "Rejected bulk equipment was partially created.");
            foreach (uint counter in new[] { uint.MaxValue, uint.MaxValue - 1 })
            {
                byte[] overflow = (byte[])bytes.Clone();
                BinaryPrimitives.WriteUInt32LittleEndian(overflow.AsSpan(0x46908), counter);
                var protectedSave = SaveDocument.Parse(overflow);
                reject(() => protectedSave.FillMissingEquipment(EquipmentSlot.Weapon, character), "Bulk serial overflow was accepted.");
                check(protectedSave.Serialize().AsSpan().SequenceEqual(overflow), "Serial rejection partially wrote equipment.");
            }
            byte[] stale = (byte[])bulkExpected.Clone();
            BinaryPrimitives.WriteUInt32LittleEndian(stale.AsSpan(0x46908), 0);
            var staleSave = SaveDocument.Parse(stale);
            var last = staleSave.AddEquipment(targets[0].Id);
            check(BinaryPrimitives.ReadUInt32LittleEndian(staleSave.Serialize().AsSpan(Start(EquipmentSlot.Weapon) + last.Index * 0x30 + 0xc)) == targets.Length + 1,
                "Creation reused an acquisition serial when the counter was stale.");
            foreach (int invalid in new[] { -1, 0, 65535, 1, 5 })
                reject(() => original.AddEquipment(invalid), "Unknown or story equipment was created.");
            foreach (int invalid in new[] { -1, 500 })
                reject(() => original.GetInventoryEquipment(EquipmentSlot.Head, invalid), "Invalid equipment index was exposed.");
            reject(() => original.CreatableEquipment((EquipmentSlot)6), "Invalid equipment bank was exposed.");
            reject(() => original.FillMissingEquipment(EquipmentSlot.Weapon, future ? 2 : 14), "Bulk filling accepted an absent character.");
            check(original.Serialize().AsSpan().SequenceEqual(bytes), "Rejected equipment requests changed bytes.");
            foreach (int version in new[] { 0, 6, 8 })
            {
                byte[] unsupported = (byte[])bytes.Clone();
                BinaryPrimitives.WriteUInt32LittleEndian(unsupported, (uint)version);
                var protectedSave = SaveDocument.Parse(unsupported);
                check(!protectedSave.CanEditInventoryEquipment && protectedSave.CreatableEquipment(EquipmentSlot.Weapon).Count == 0, "Unverified format exposes equipment creation.");
                reject(() => protectedSave.AddEquipment(targets[0].Id), "Unverified format accepted equipment creation.");
                reject(() => protectedSave.FillMissingEquipment(EquipmentSlot.Weapon, character), "Unverified format accepted bulk creation.");
                check(protectedSave.Serialize().AsSpan().SequenceEqual(unsupported), "Unverified equipment rejection changed bytes.");
            }
            byte[] ambiguous = (byte[])bytes.Clone();
            ambiguous.AsSpan(0x152318, 25).Clear();
            BinaryPrimitives.WriteUInt16LittleEndian(ambiguous.AsSpan(0x152318), 1);
            ambiguous[0x152330] = 1;
            var unknown = SaveDocument.Parse(ambiguous);
            check(unknown.Campaign == Campaign.Unknown && unknown.CreatableEquipment(EquipmentSlot.Weapon).Count == 0,
                "Unconfirmed campaign exposes equipment creation.");
            reject(() => unknown.AddEquipment(targets[0].Id), "Unconfirmed campaign accepted equipment creation.");
            check(unknown.Serialize().AsSpan().SequenceEqual(ambiguous), "Unconfirmed campaign rejection changed bytes.");
        }
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var save = SaveDocument.Parse(bytes);
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            if (!save.CanEditInventoryEquipment) continue;
            var definition = save.CreatableEquipment(slot).First();
            var copy = SaveDocument.Parse(bytes);
            var added = copy.AddEquipment(definition.Id);
            byte[] actual = copy.Serialize();
            int start = Start(slot) + added.Index * 0x30;
            int counter = 0x46900 + Type(slot) * 4;
            check(added.IsValid && added.EquippedBy.Count == 0, "Real-save creation reused an equipped record.");
            byte[] expected = (byte[])bytes.Clone();
            actual.AsSpan(start, 0x30).CopyTo(expected.AsSpan(start));
            actual.AsSpan(counter, 4).CopyTo(expected.AsSpan(counter));
            check(actual.AsSpan().SequenceEqual(expected), "Real-save creation changed another equipment record or a character.");
        }
        check(save.Serialize().AsSpan().SequenceEqual(bytes), "Equipment inventory inspection changed the real save.");
    }

    private static void Initialize(byte[] bytes, EquipmentDefinition definition, int index, uint serial)
    {
        int start = Start(definition.Slot) + index * 0x30;
        bytes.AsSpan(start, 0x30).Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start), (ushort)index);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start + 2), (ushort)Type(definition.Slot));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start + 4), (ushort)definition.Id);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start + 6), (ushort)Type(definition.Slot));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start + 8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(start + 0xc), serial);
        bytes[start + 0x10] = 1;
        bytes[start + 0x14] = (byte)definition.ArmorClass;
        bytes[start + 0x15] = (byte)definition.GemSlotCount;
        for (int socket = 1; socket <= 3; socket++)
            if (definition.FixedGem(socket) is > 0 and var fixedId)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(start + 0x1c + (socket - 1) * 8), (uint)fixedId | (1u << 16));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x46900 + Type(definition.Slot) * 4), serial);
    }

    private static int Start(EquipmentSlot slot) => new[] { 0x3b10, 0x98d0, 0xf690, 0x15450, 0x1b210, 0x20fd0 }[(int)slot];
    private static int Type(EquipmentSlot slot) => slot == EquipmentSlot.Weapon ? 2 : (int)slot + 3;
    private static int Reference(int id, EquipmentSlot slot) => 0x152368 + (id - 1) * 0x138 + (slot == EquipmentSlot.Weapon ? 0x28 : 0x10 + (int)slot * 4);
}
