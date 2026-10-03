using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class EquipmentTests
{
    internal const int Weapon = 0x3b10;
    internal const int Gem = 0x2c380;
    internal const int Character = 0x152368;

    internal static byte[] Fixture(byte[] data)
    {
        data.AsSpan(0x3b10, 0x31970 - 0x3b10).Clear();
        for (int index = 0; index < 3; index++)
        {
            int offset = Weapon + index * 0x30;
            Put16(data, offset, index);
            Put16(data, offset + 2, 2);
            Put16(data, offset + 4, 15);
            Put16(data, offset + 6, 2);
            Put16(data, offset + 8, 1);
            data[offset + 0x10] = 1;
            data[offset + 0x15] = 3;
            Put32(data, offset + 0x18, (uint)index | (3u << 16));
        }
        Put16(data, Character + 0x28, 0);
        Put16(data, Character + 0x2a, 2);
        Put16(data, Character + 0x138 + 0x28, 1);
        Put16(data, Character + 0x138 + 0x2a, 2);
        Put32(data, Weapon + 0x24, 3297u | (1u << 16));
        for (int index = 0; index < 6; index++)
        {
            int offset = Gem + index * 0x2c;
            Put16(data, offset, index);
            Put16(data, offset + 2, 3);
            Put16(data, offset + 6, 3);
            Put16(data, offset + 8, 1);
            data[offset + 0x10] = 1;
            data[offset + 0x16] = 6;
            Put16(data, offset + 0x1a, 1);
            Put16(data, offset + 0x1c, 1);
            Put16(data, offset + 0x1e, 100);
        }
        data[Gem + 5 * 0x2c + 0x19] = 1;
        return data;
    }

    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        foreach (bool future in new[] { false, true })
        {
            byte[] bytes = Fixture(fixture(future));
            var save = SaveDocument.Parse(bytes);
            var equipment = save.GetCharacter(1).GetEquipment(EquipmentSlot.Weapon);
            check(equipment.Exists && equipment.Index == 0 && equipment.ItemId == 15 && equipment.CanEdit,
                "Equipment reference does not resolve to the zero-based inventory slot.");
            check(save.Serialize().AsSpan().SequenceEqual(bytes), "Equipment inspection changed a save.");
            check(equipment.GemSockets[0].GemIndex == 0 && !equipment.GemSockets[0].IsEmpty,
                "Gem zero was confused with an empty socket.");
            check(equipment.GemSockets[1].FixedItemId == 3297 && !equipment.GemSockets[1].CanEdit,
                "A built-in gem is editable.");
            check(equipment.GemSockets[2].IsEmpty && equipment.GemSockets[2].CanEdit,
                "An existing empty socket cannot receive a gem.");
            check(equipment.GemSockets[0].AvailableGems.Select(gem => gem.Index).SequenceEqual([0, 3, 4]),
                "Gem options include another character's gem, an unequipped item's gem or a cylinder.");
            foreach (int invalid in new[] { -1, 1, 2, 5, 499, 500 })
                reject(() => equipment.SetGems([invalid, null, null]), "Unavailable or invalid gem was fitted.");
            reject(() => equipment.SetGems([3, null, 3]), "One gem occupied two sockets.");
            reject(() => equipment.SetGems([0, 3, null]), "Fixed gem was overwritten.");
            reject(() => equipment.SetGems([0]), "Socket count was ignored.");
            check(save.Serialize().AsSpan().SequenceEqual(bytes), "Rejected equipment edit partially changed bytes.");
            equipment.SetGems([3, null, 4]);
            byte[] edited = save.Serialize();
            check(equipment.GemSockets[0].GemIndex == 3 && equipment.GemSockets[2].GemIndex == 4,
                "Gem references did not change together.");
            check(Enumerable.Range(0, bytes.Length).All(offset => bytes[offset] == edited[offset]
                || offset is >= Weapon + 0x18 and < Weapon + 0x1c or >= Weapon + 0x28 and < Weapon + 0x2c),
                "Gem editing altered fixed gems, inventory records, cosmetics or character stats.");
            equipment.SetGems([3, null, 4]);
            check(save.Serialize().AsSpan().SequenceEqual(edited), "Gem fitting is not idempotent.");
            equipment.SetGems([null, null, null]);
            check(equipment.GemSockets[0].IsEmpty && equipment.GemSockets[2].IsEmpty
                && equipment.GemSockets[1].FixedItemId == 3297, "Removing normal gems changed a fixed gem.");

            foreach (int malformed in new[] { 500, 65535 })
            {
                byte[] bad = (byte[])bytes.Clone();
                Put16(bad, Character + 0x28, malformed);
                var missing = SaveDocument.Parse(bad).GetCharacter(1).GetEquipment(EquipmentSlot.Weapon);
                check(!missing.Exists && missing.GemSockets.Count == 0 && missing.Name.Contains(malformed.ToString()),
                    "Malformed equipment references were dereferenced or hidden.");
                reject(() => missing.SetGems([]), "Malformed equipment could be edited.");
            }
            byte[] oversized = (byte[])bytes.Clone();
            oversized[Weapon + 0x15] = 4;
            reject(() => SaveDocument.Parse(oversized).GetCharacter(1).GetEquipment(EquipmentSlot.Weapon)
                .SetGems([3, null, null]), "More than three gem slots were accepted.");
            byte[] stacked = (byte[])bytes.Clone();
            Put16(stacked, Weapon + 8, 2);
            reject(() => SaveDocument.Parse(stacked).GetCharacter(1).GetEquipment(EquipmentSlot.Weapon)
                .SetGems([3, null, null]), "An invalid equipment quantity accepted gem editing.");
            reject(() => save.GetCharacter(1).GetEquipment((EquipmentSlot)100), "Invalid equipment slot was accepted.");
            if (!future)
            {
                byte[] shared = (byte[])bytes.Clone();
                Put16(shared, Character + 0x138 + 0x28, 0);
                reject(() => SaveDocument.Parse(shared).GetCharacter(1).GetEquipment(EquipmentSlot.Weapon)
                    .SetGems([3, null, null]), "Shared equipment references were edited ambiguously.");
            }
        }
        byte[] unknown = Fixture(fixture(false));
        unknown[0x152330] = 1;
        var unsupported = SaveDocument.Parse(unknown);
        reject(() => unsupported.GetCharacter(1).GetEquipment(EquipmentSlot.Weapon).SetGems([3, null, null]),
            "An unidentified campaign accepted equipment editing.");
        check(unsupported.Serialize().AsSpan().SequenceEqual(unknown), "Unsupported equipment editing changed bytes.");
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var save = SaveDocument.Parse(bytes);
        check(save.Characters.SelectMany(character => character.Equipment).All(equipment => equipment.Exists),
            "An actual equipped item failed inventory-reference validation.");
        foreach (var equipment in save.Characters.SelectMany(character => character.Equipment))
        {
            foreach (var socket in equipment.GemSockets.Where(socket => socket.CanEdit))
            {
                var copy = SaveDocument.Parse(bytes);
                var item = copy.GetCharacter(equipment.CharacterId).GetEquipment(equipment.Slot);
                var choices = item.GemSockets[socket.Index - 1].AvailableGems;
                int? replacement = choices.FirstOrDefault(gem => gem.Index != socket.GemIndex)?.Index;
                item.SetGems(item.GemSockets.Select(current => current.Index == socket.Index ? replacement : current.GemIndex).ToArray());
                byte[] edited = copy.Serialize();
                int start = EquipmentOffset(equipment.Slot) + equipment.Index * 0x30 + 0x18 + (socket.Index - 1) * 8;
                check(edited.AsSpan(0, start).SequenceEqual(bytes.AsSpan(0, start))
                    && edited.AsSpan(start + 4).SequenceEqual(bytes.AsSpan(start + 4)),
                    "Real-save gem edit changed data outside one socket.");
            }
        }
        check(save.Serialize().AsSpan().SequenceEqual(bytes), "Real-save equipment inspection changed original data.");
    }

    private static int EquipmentOffset(EquipmentSlot slot) => new[] { 0x3b10, 0x98d0, 0xf690, 0x15450, 0x1b210, 0x20fd0 }[(int)slot];
    private static void Put16(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), (ushort)value);
    private static void Put32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
