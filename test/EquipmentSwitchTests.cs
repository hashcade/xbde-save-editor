using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class EquipmentSwitchTests
{
    internal static byte[] Fixture(byte[] bytes)
    {
        EquipmentTests.Fixture(bytes);
        for (int id = 1; id <= 15; id++)
        {
            int offset = 0x152368 + (id - 1) * 0x138;
            bytes.AsSpan(offset + 0x14, 0x18).Clear();
            for (int slot = 0; slot < 6; slot++)
                Put32(bytes, offset + (slot == 0 ? 0x28 : 0x10 + slot * 4), 0xffff);
            bytes.AsSpan(offset + 0x90, 0x14).Clear();
            bytes.AsSpan(offset + 0xc4, 40).Clear();
        }
        SetItem(bytes, EquipmentSlot.Weapon, 0, 2);
        SetItem(bytes, EquipmentSlot.Weapon, 1, 15);
        SetItem(bytes, EquipmentSlot.Weapon, 2, 2);
        SetItem(bytes, EquipmentSlot.Weapon, 3, 5);
        SetItem(bytes, EquipmentSlot.Weapon, 4, 534);
        SetItem(bytes, EquipmentSlot.Head, 0, 837, 1);
        SetItem(bytes, EquipmentSlot.Head, 1, 837, 1);
        SetItem(bytes, EquipmentSlot.Head, 2, 1087, 2);
        SetItem(bytes, EquipmentSlot.Head, 3, 1052, 3);
        SetItem(bytes, EquipmentSlot.Head, 4, 761, 13);
        Put32(bytes, 0x152368 + 0x28, 2u << 16);
        Put32(bytes, 0x152368 + 0x14, 4u << 16);
        Put32(bytes, 0x1524a0 + 0x28, 1u | (2u << 16));
        return bytes;
    }

    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        foreach (bool future in new[] { false, true })
        {
            byte[] bytes = Fixture(fixture(future));
            var save = SaveDocument.Parse(bytes);
            var weapon = save.GetCharacter(1).GetEquipment(EquipmentSlot.Weapon);
            check(weapon.CanSwitch && weapon.AvailableItems.Select(item => item.Index).SequenceEqual([0, 2]),
                "Weapon options include another character's weapon or a story-flagged Monado.");
            foreach (int invalid in new[] { -1, 1, 3, 4, 499, 500 })
                reject(() => weapon.Equip(invalid), "Invalid or unavailable weapon was equipped.");
            check(save.Serialize().AsSpan().SequenceEqual(bytes), "Rejected switching changed a save.");
            weapon.Equip(2);
            AssertOnlyReferenceChanged(bytes, save.Serialize(), 1, EquipmentSlot.Weapon, check);
            check(weapon.Index == 2 && weapon.GemSockets[0].GemIndex == 2,
                "Switching did not retain the target weapon's gem.");
            weapon.Equip(0);
            check(save.Serialize().AsSpan().SequenceEqual(bytes), "Switching back did not restore the exact save.");
            var head = save.GetCharacter(1).GetEquipment(EquipmentSlot.Head);
            check(head.AvailableItems.Select(item => item.Index).SequenceEqual(future ? [0, 1, 2, 3] : [0, 1]),
                "Armor permissions do not distinguish main story from Future Connected.");
            head.Equip(1);
            AssertOnlyReferenceChanged(bytes, save.Serialize(), 1, EquipmentSlot.Head, check);

            foreach (int field in new[] { 0, 2, 6, 8, 0x10, 0x15 })
            {
                var malformed = (byte[])bytes.Clone();
                malformed[0x3b10 + 2 * 0x30 + field] = 255;
                var item = SaveDocument.Parse(malformed).GetCharacter(1).GetEquipment(EquipmentSlot.Weapon);
                reject(() => item.Equip(2), "Malformed inventory equipment was accepted.");
            }
            var wrongClass = (byte[])bytes.Clone();
            wrongClass[0x98d0 + 0x30 + 0x14] = 3;
            reject(() => SaveDocument.Parse(wrongClass).GetCharacter(1).GetEquipment(EquipmentSlot.Head).Equip(1),
                "Armor class inconsistent with the game table was accepted.");
            var story = (byte[])bytes.Clone();
            Put32(story, 0x152368 + 0x28, 3u | (2u << 16));
            var protectedWeapon = SaveDocument.Parse(story).GetCharacter(1).GetEquipment(EquipmentSlot.Weapon);
            check(!protectedWeapon.CanSwitch && protectedWeapon.AvailableItems.Count == 0, "Story weapon is switchable.");
            reject(() => protectedWeapon.Equip(0), "Story weapon restriction was bypassed.");
        }

        byte[] main = Fixture(fixture(false));
        Put32(main, 0x152368 + 0x90, 1);
        var own = SaveDocument.Parse(main);
        check(own.GetCharacter(1).GetEquipment(EquipmentSlot.Head).AvailableItems.Any(item => item.Index == 2),
            "Learned medium-equipment skill was ignored.");
        Put32(main, 0x152368 + 5 * 0x138 + 0x90 + 3 * 4, 1);
        main[0x152368 + 0xc4 + 5 * 5] = 141;
        main[0x50 + ((0x2cdd + 11) >> 3)] |= (byte)(1 << ((0x2cdd + 11) & 7));
        var linked = SaveDocument.Parse(main);
        check(linked.GetCharacter(1).GetEquipment(EquipmentSlot.Head).AvailableItems.Any(item => item.Index == 3),
            "Existing learned heavy-equipment skill link was ignored.");
        var unlearned = (byte[])main.Clone();
        Put32(unlearned, 0x152368 + 5 * 0x138 + 0x90 + 3 * 4, 0);
        check(!SaveDocument.Parse(unlearned).GetCharacter(1).GetEquipment(EquipmentSlot.Head).AvailableItems.Any(item => item.Index == 3),
            "Linking an unlearned donor skill granted armor permission.");
        var locked = (byte[])main.Clone();
        locked[0x50 + ((0x2cdd + 11) >> 3)] &= (byte)(255 ^ (1 << ((0x2cdd + 11) & 7)));
        check(!SaveDocument.Parse(locked).GetCharacter(1).GetEquipment(EquipmentSlot.Head).AvailableItems.Any(item => item.Index == 3),
            "A skill in a locked donor branch granted armor permission.");
        main[0x152368 + 0xc4 + 5 * 5] = 0;
        check(!SaveDocument.Parse(main).GetCharacter(1).GetEquipment(EquipmentSlot.Head).AvailableItems.Any(item => item.Index == 3),
            "Merely learning another character's heavy skill granted permission without a link.");
        main[0x152368 + 0xc4] = 141;
        check(!SaveDocument.Parse(main).GetCharacter(1).GetEquipment(EquipmentSlot.Head).AvailableItems.Any(item => item.Index == 3),
            "A link in the wrong donor row granted armor permission.");
        Put32(main, 0x152368 + 6 * 0x138 + 0x14, 1u | (4u << 16));
        main[0x152368 + 6 * 0x138 + 0xc4] = 1;
        check(SaveDocument.Parse(main).GetCharacter(7).GetEquipment(EquipmentSlot.Head).AvailableItems.Any(item => item.Index == 2),
            "The first (Shulk) skill-link row was omitted.");
        Put32(main, 0x152368 + 7 * 0x138 + 0x14, 4u | (4u << 16));
        Put32(main, 0x152368 + 7 * 0x138 + 0x28, 4u | (2u << 16));
        var fiora = SaveDocument.Parse(main).GetCharacter(8);
        check(fiora.GetEquipment(EquipmentSlot.Head).AvailableItems.Select(item => item.Index).SequenceEqual([4]),
            "Mechon Fiora can equip normal armor.");
        check(fiora.GetEquipment(EquipmentSlot.Weapon).AvailableItems.Select(item => item.Index).SequenceEqual([4]),
            "Mechon Fiora can equip another character's weapon.");
        var human = (byte[])main.Clone();
        Put16(human, 0x152318 + 2 * 2, 3);
        Put32(human, 0x152368 + 2 * 0x138, 99);
        Put32(human, 0x152368 + 2 * 0x138 + 0x14, 2u | (4u << 16));
        Put32(human, 0x152368 + 2 * 0x138 + 0x90, 1);
        human[0x152368 + 2 * 0x138 + 0xc4 + 5 * 5] = 141;
        var humanHead = SaveDocument.Parse(human).GetCharacter(3).GetEquipment(EquipmentSlot.Head);
        check(humanHead.CanSwitch && !humanHead.AvailableItems.Any(item => item.Index is 3 or 4),
            "Human Fiora can equip heavy or Mechon armor.");
        var occupied = Fixture(fixture(false));
        Put32(occupied, 0x1524a0 + 0x28, 2);
        reject(() => SaveDocument.Parse(occupied).GetCharacter(1).GetEquipment(EquipmentSlot.Weapon).Equip(2),
            "A malformed equipment reference did not reserve the occupied item.");
        var unknown = Fixture(fixture(false));
        unknown[0x152330] = 1;
        reject(() => SaveDocument.Parse(unknown).GetCharacter(1).GetEquipment(EquipmentSlot.Weapon).Equip(2),
            "An unidentified campaign accepted equipment switching.");
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var save = SaveDocument.Parse(bytes);
        foreach (var character in save.Characters)
            foreach (var equipment in character.Equipment)
            {
                if (equipment.Slot != EquipmentSlot.Weapon || character.Id != 1)
                    check(equipment.CanSwitch, "A real, compatible equipped item was incorrectly made read-only.");
                foreach (var choice in equipment.AvailableItems)
                {
                    var copy = SaveDocument.Parse(bytes);
                    var changed = copy.GetCharacter(character.Id).GetEquipment(equipment.Slot);
                    changed.Equip(choice.Index);
                    check(changed.Index == choice.Index && changed.ItemId == choice.ItemId, "Real equipment reference did not resolve.");
                    AssertOnlyReferenceChanged(bytes, copy.Serialize(), character.Id, equipment.Slot, check);
                    changed.Equip(equipment.Index);
                    check(copy.Serialize().AsSpan().SequenceEqual(bytes), "Switching real equipment back was not lossless.");
                }
            }
        check(save.Serialize().AsSpan().SequenceEqual(bytes), "Inspection changed the real save.");
    }

    private static void AssertOnlyReferenceChanged(byte[] before, byte[] after, int character, EquipmentSlot slot, Action<bool, string> check)
    {
        int offset = 0x152368 + (character - 1) * 0x138 + (slot == EquipmentSlot.Weapon ? 0x28 : 0x10 + (int)slot * 4);
        check(before.AsSpan(0, offset).SequenceEqual(after.AsSpan(0, offset))
            && before.AsSpan(offset + 4).SequenceEqual(after.AsSpan(offset + 4)),
            "Equipment switching altered inventory, gems, cosmetics or character stats.");
    }

    private static void SetItem(byte[] bytes, EquipmentSlot slot, int index, int itemId, int armorClass = 0)
    {
        int offset = new[] { 0x3b10, 0x98d0, 0xf690, 0x15450, 0x1b210, 0x20fd0 }[(int)slot] + index * 0x30;
        if (slot != EquipmentSlot.Weapon) bytes.AsSpan(offset, 0x30).Clear();
        int type = slot == EquipmentSlot.Weapon ? 2 : (int)slot + 3;
        Put16(bytes, offset, index);
        Put16(bytes, offset + 2, type);
        Put16(bytes, offset + 4, itemId);
        Put16(bytes, offset + 6, type);
        Put16(bytes, offset + 8, 1);
        bytes[offset + 0x10] = 1;
        bytes[offset + 0x14] = (byte)armorClass;
    }
    private static void Put16(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), (ushort)value);
    private static void Put32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
