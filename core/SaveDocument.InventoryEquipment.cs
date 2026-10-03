namespace XbdeEditor.Core;

public sealed partial class SaveDocument
{
    public bool CanEditInventoryEquipment => Campaign != Campaign.Unknown && ReadUInt32(0) == 7;

    public InventoryEquipmentRecord GetInventoryEquipment(EquipmentSlot slot, int index)
    {
        if (!Enum.IsDefined(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
        if (index is < 0 or >= InventoryCatalog.Capacity) throw new ArgumentOutOfRangeException(nameof(index));
        return new(this, slot, index);
    }

    public IReadOnlyList<InventoryEquipmentRecord> InventoryEquipment(EquipmentSlot slot)
    {
        if (!Enum.IsDefined(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
        return Enumerable.Range(0, InventoryCatalog.Capacity).Select(index => GetInventoryEquipment(slot, index))
            .Where(item => item.Exists).ToArray();
    }

    public IReadOnlyList<EquipmentDefinition> CreatableEquipment(EquipmentSlot slot)
    {
        if (!Enum.IsDefined(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
        if (!CanEditInventoryEquipment) return [];
        return EquipmentDefinitions.All.Where(item => item.Slot == slot && item.IsOrdinary
            && item.Characters.Any(id => Campaign == Campaign.MainStory ? id is >= 1 and <= 8 : id is 1 or 7 or 14 or 15)).ToArray();
    }

    public InventoryEquipmentRecord AddEquipment(int itemId)
    {
        var definition = EquipmentDefinitions.Find(itemId) ?? throw new ArgumentException("Unrecognized equipment.", nameof(itemId));
        if (!CreatableEquipment(definition.Slot).Any(item => item.Id == itemId))
            throw new ArgumentException("This equipment cannot be created in this campaign.");
        return CreateEquipmentBatch([definition])[0];
    }

    public IReadOnlyList<InventoryEquipmentRecord> FillMissingEquipment(EquipmentSlot slot, int characterId)
    {
        if (!CanEditInventoryEquipment) throw new ArgumentException("Equipment inventory edits require an identified campaign and save format 7.");
        GetCharacter(characterId);
        var owned = InventoryEquipment(slot).Select(item => item.ItemId).ToHashSet();
        var missing = CreatableEquipment(slot).Where(item => item.Characters.Contains(characterId) && !owned.Contains(item.Id)).ToArray();
        return CreateEquipmentBatch(missing);
    }

    internal HashSet<int> ReferencedEquipmentIndices(EquipmentSlot slot)
    {
        int type = EquipmentCatalog.Type(slot);
        var joined = PartyIds.ToHashSet();
        var indices = new HashSet<int>();
        for (int id = 1; id <= 15; id++)
        {
            int offset = CharacterOffset + (id - 1) * CharacterSize + EquipmentCatalog.CharacterField(slot);
            int index = ReadUInt16(offset);
            if (index < InventoryCatalog.Capacity && (ReadUInt16(offset + 2) == type || joined.Contains(id))) indices.Add(index);
        }
        return indices;
    }

    private IReadOnlyList<InventoryEquipmentRecord> CreateEquipmentBatch(IReadOnlyList<EquipmentDefinition> definitions)
    {
        var writes = new List<(EquipmentDefinition Definition, int Index, uint Serial)>();
        foreach (var group in definitions.GroupBy(item => item.Slot))
        {
            var items = group.ToArray();
            int start = EquipmentCatalog.InventoryOffset(group.Key);
            var reserved = ReferencedEquipmentIndices(group.Key);
            var free = Enumerable.Range(0, InventoryCatalog.Capacity)
                .Where(index => ReadByte(start + index * EquipmentRecord.InventorySize + 0x10) == 0 && !reserved.Contains(index))
                .Take(items.Length).ToArray();
            if (free.Length != items.Length) throw new ArgumentException("There are not enough unreferenced inventory slots for this equipment batch.");
            uint firstSerial = NextInventorySerial(EquipmentCatalog.Type(group.Key), start, EquipmentRecord.InventorySize);
            if ((ulong)firstSerial + (uint)items.Length - 1 > uint.MaxValue)
                throw new ArgumentException("Inventory serial counter cannot accommodate this equipment batch.");
            for (int index = 0; index < items.Length; index++) writes.Add((items[index], free[index], firstSerial + (uint)index));
        }
        foreach (var (definition, index, serial) in writes)
        {
            int type = EquipmentCatalog.Type(definition.Slot);
            int offset = EquipmentCatalog.InventoryOffset(definition.Slot) + index * EquipmentRecord.InventorySize;
            _data.AsSpan(offset, EquipmentRecord.InventorySize).Clear();
            WriteUInt16(offset, (ushort)index);
            WriteUInt16(offset + 2, (ushort)type);
            WriteUInt16(offset + 4, (ushort)definition.Id);
            WriteUInt16(offset + 6, (ushort)type);
            WriteUInt16(offset + 8, 1);
            WriteUInt32(offset + 0xc, serial);
            WriteByte(offset + 0x10, 1);
            WriteByte(offset + 0x14, (byte)definition.ArmorClass);
            WriteByte(offset + 0x15, (byte)definition.GemSlotCount);
            for (int socket = 1; socket <= 3; socket++)
                if (definition.FixedGem(socket) is > 0 and var fixedId)
                    WriteUInt32(offset + 0x1c + (socket - 1) * 8, (uint)fixedId | (1u << 16));
            WriteUInt32(InventorySerialOffset(type), serial);
        }
        return writes.Select(write => GetInventoryEquipment(write.Definition.Slot, write.Index)).ToArray();
    }
}
