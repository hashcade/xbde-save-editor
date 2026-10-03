using System.Buffers.Binary;
using System.Security.Cryptography;

namespace XbdeEditor.Core;

public enum Campaign { MainStory, FutureConnected, Unknown }

public sealed partial class SaveDocument
{
    public const int FileSize = 0x153860;
    internal const int PartyOffset = 0x152318;
    internal const int CharacterOffset = 0x152368;
    internal const int CharacterSize = 0x138;
    internal const int ArtsOffset = 0x1536e8;
    private readonly byte[] _data;

    private SaveDocument(byte[] data)
    {
        _data = data;
        ValidateLayout();
    }

    public static SaveDocument Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != FileSize)
            throw new InvalidDataException($"Expected a {FileSize:N0}-byte game save. System saves and thumbnails are not supported.");
        return new SaveDocument(data.ToArray());
    }

    public IReadOnlyList<int> PartyIds => Enumerable.Range(0, _data[PartyOffset + 24])
        .Select(index => (int)ReadUInt16(PartyOffset + index * 2)).ToArray();

    public Campaign Campaign
    {
        get
        {
            var ids = PartyIds;
            if (ids.Any(id => id >= 14)) return Campaign.FutureConnected;
            if (ids.Any(id => id is not (1 or 7))) return Campaign.MainStory;
            return Campaign.Unknown;
        }
    }

    public IReadOnlyList<CharacterRecord> Characters => PartyIds.Where(id => id <= 15)
        .Select(id => new CharacterRecord(this, id)).ToArray();

    public CharacterRecord GetCharacter(int id) => Characters.FirstOrDefault(character => character.Id == id)
        ?? throw new ArgumentException("Only characters already present in this party can be edited.", nameof(id));

    public string Sha256 => Convert.ToHexString(SHA256.HashData(_data));
    public uint Money => ReadUInt32(0x151b40);
    public uint Noponstones => ReadUInt32(0x10);

    public void SetResources(uint money, uint noponstones)
    {
        WriteUInt32(0x151b40, money);
        WriteUInt32(0x10, noponstones);
    }

    public byte[] Serialize() => (byte[])_data.Clone();
    public bool CanEditAchievements => Campaign == Campaign.MainStory && ReadUInt32(0) == 7;
    public bool CanEditSkillLinks => Campaign == Campaign.MainStory && ReadUInt32(0) == 7;
    public bool CanEditAffinity => Campaign == Campaign.MainStory && ReadUInt32(0) == 7;
    public bool CanEditRegionAffinity => Campaign == Campaign.MainStory && ReadUInt32(0) == 7;
    public IReadOnlyList<RegionAffinityRecord> RegionAffinities => Campaign == Campaign.MainStory
        ? RegionAffinityRecord.Definitions.Select(definition => new RegionAffinityRecord(this, definition)).ToArray() : [];

    public RegionAffinityRecord GetRegionAffinity(int id) => RegionAffinities.FirstOrDefault(region => region.Id == id)
        ?? throw new ArgumentException("Choose one of the five main-story affinity regions.", nameof(id));

    public void MaxAllRegionAffinity()
    {
        if (!CanEditRegionAffinity) throw new ArgumentException("Region affinity requires an identified main-story save with format version 7.");
        foreach (var region in RegionAffinities) region.SetPoints(RegionAffinityRecord.MaximumPoints);
    }

    public IReadOnlyList<AffinityRecord> Affinities
    {
        get
        {
            if (Campaign != Campaign.MainStory) return [];
            var ids = PartyIds.Where(id => id is >= 1 and <= 8)
                .GroupBy(AffinityCatalog.CanonicalCharacterId)
                .ToDictionary(group => group.Key, group => group.Contains(8) ? 8 : group.First());
            return AffinityCatalog.All.Where(pair => ids.ContainsKey(pair.FirstCharacterId) && ids.ContainsKey(pair.SecondCharacterId))
                .Select(pair => new AffinityRecord(this, ids[pair.FirstCharacterId], ids[pair.SecondCharacterId])).ToArray();
        }
    }

    public AffinityRecord GetAffinity(int firstId, int secondId)
    {
        if (!PartyIds.Contains(firstId) || !PartyIds.Contains(secondId))
            throw new ArgumentException("Only affinity between joined characters can be edited.");
        int index = AffinityCatalog.Get(firstId, secondId).Index;
        return Affinities.FirstOrDefault(pair => pair.Index == index)
            ?? throw new ArgumentException("This campaign does not have character affinity.");
    }

    public void MaxAllAffinity()
    {
        if (!CanEditAffinity) throw new ArgumentException("Affinity requires an identified main-story save with format version 7.");
        foreach (var pair in Affinities) pair.SetPoints(AffinityCatalog.MaximumPoints);
    }
    internal bool IsSkillLinkSourceAvailable(int id)
    {
        if (id is < 1 or > 8) return false;
        if ((ReadByte(0x5eb) & 0x10) != 0) return id != 3;
        int story = ReadUInt16(0xdf0);
        return id switch
        {
            1 or 2 => true,
            3 => story is >= 11 and < 42,
            4 => story >= 100,
            5 => story >= 69,
            6 => story >= 137,
            7 => story >= 128,
            8 => story >= 273,
            _ => false
        };
    }
    public IReadOnlyList<AchievementRecord> Achievements => AchievementCatalog.All
        .Select(definition => new AchievementRecord(this, definition)).ToArray();
    public AchievementRecord GetAchievement(int id) => new(this, AchievementCatalog.Get(id));

    public void UnlockAllAchievements()
    {
        if (!CanEditAchievements)
            throw new ArgumentException("Achievements require a confirmed main-story save with format version 7.");
        foreach (var achievement in Achievements) achievement.Unlock();
    }

    public IReadOnlyList<InventoryItemRecord> Inventory(InventoryKind kind) => Enumerable.Range(0, InventoryCatalog.Capacity)
        .Select(index => GetInventoryItem(kind, index)).Where(item => item.Exists).ToArray();

    public InventoryItemRecord GetInventoryItem(InventoryKind kind, int index)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (index is < 0 or >= InventoryCatalog.Capacity) throw new ArgumentOutOfRangeException(nameof(index));
        return new(this, kind, index);
    }

    public InventoryItemRecord AddInventoryItem(int itemId, int quantity)
    {
        var definition = InventoryCatalog.Find(itemId) ?? throw new ArgumentException("Unrecognized item.");
        if (Campaign == Campaign.Unknown || definition.Kind == InventoryKind.KeyItems)
            throw new ArgumentException("Adding quest items or items to an unconfirmed campaign is not supported.");
        if (quantity is < 1 or > InventoryCatalog.MaximumQuantity) throw new ArgumentOutOfRangeException(nameof(quantity));
        var matches = Inventory(definition.Kind).Where(item => item.ItemId == itemId).ToArray();
        if (matches.Length > 1) throw new ArgumentException("Duplicate stacks must be resolved before adding this item.");
        if (matches.Length == 1)
        {
            var existing = matches[0];
            existing.SetQuantity(checked(existing.Quantity + quantity));
            return existing;
        }
        int index = FindFreeInventorySlot(InventoryCatalog.Offset(definition.Kind), InventoryItemRecord.Size);
        uint serial = NextInventorySerial((int)definition.Kind, InventoryCatalog.Offset(definition.Kind), InventoryItemRecord.Size);
        int offset = InventoryCatalog.Offset(definition.Kind) + index * InventoryItemRecord.Size;
        _data.AsSpan(offset, InventoryItemRecord.Size).Clear();
        WriteUInt16(offset, (ushort)index);
        WriteUInt16(offset + 2, (ushort)definition.Kind);
        WriteUInt16(offset + 4, (ushort)itemId);
        WriteUInt16(offset + 6, (ushort)definition.Kind);
        WriteUInt16(offset + 8, (ushort)quantity);
        WriteUInt32(offset + 0xc, serial);
        WriteByte(offset + 0x10, 1);
        WriteUInt32(InventorySerialOffset((int)definition.Kind), serial);
        return new(this, definition.Kind, index);
    }

    public void MaxInventoryQuantities(InventoryKind kind)
    {
        var items = Inventory(kind);
        if (Campaign == Campaign.Unknown || kind == InventoryKind.KeyItems || items.Any(item => !item.CanEdit))
            throw new ArgumentException("This inventory contains protected or unrecognized records.");
        foreach (var item in items) item.SetQuantity(InventoryCatalog.MaximumQuantity);
    }

    internal int FindFreeInventorySlot(int start, int size, IReadOnlySet<int>? reserved = null)
    {
        for (int index = 0; index < InventoryCatalog.Capacity; index++)
            if (ReadByte(start + index * size + 0x10) == 0 && reserved?.Contains(index) != true) return index;
        throw new ArgumentException("This inventory is full.");
    }

    internal static int InventorySerialOffset(int type) => 0x46900 + type * 4;
    internal uint NextInventorySerial(int type, int start, int size)
    {
        uint largest = ReadUInt32(InventorySerialOffset(type));
        for (int index = 0; index < InventoryCatalog.Capacity; index++)
            if (ReadByte(start + index * size + 0x10) != 0)
                largest = Math.Max(largest, ReadUInt32(start + index * size + 0xc));
        if (largest == uint.MaxValue) throw new ArgumentException("Inventory serial counter cannot be incremented.");
        return largest + 1;
    }

    public GemRecord AddGem(int effectId, int rank, int strength)
    {
        if (Campaign == Campaign.Unknown) throw new ArgumentException("Unconfirmed campaign.");
        var rule = GemCatalog.Find(effectId, rank) ?? throw new ArgumentException("Unrecognized gem definition.");
        if (strength < rule.Minimum || strength > rule.Maximum) throw new ArgumentOutOfRangeException(nameof(strength));
        int index = FindFreeInventorySlot(GemRecord.InventoryOffset, GemRecord.Size, UsedGemIndices());
        uint serial = NextInventorySerial(3, GemRecord.InventoryOffset, GemRecord.Size);
        int offset = GemRecord.InventoryOffset + index * GemRecord.Size;
        _data.AsSpan(offset, GemRecord.Size).Clear();
        WriteUInt16(offset, (ushort)index);
        WriteUInt16(offset + 2, 3);
        WriteUInt16(offset + 6, 3);
        WriteUInt16(offset + 8, 1);
        WriteUInt32(offset + 0xc, serial);
        WriteByte(offset + 0x10, 1);
        WriteByte(offset + 0x16, (byte)rank);
        WriteByte(offset + 0x17, (byte)rule.Attribute);
        WriteUInt16(offset + 0x1a, 1);
        WriteUInt16(offset + 0x1c, (ushort)effectId);
        WriteUInt16(offset + 0x1e, (ushort)(strength | rule.Chance << 8));
        WriteUInt32(InventorySerialOffset(3), serial);
        return GetGem(index);
    }
    public IReadOnlyList<GemRecord> Gems => Enumerable.Range(0, 500).Select(index => new GemRecord(this, index))
        .Where(gem => gem.Exists).ToArray();
    public GemRecord GetGem(int index) => index is >= 0 and < 500 ? new GemRecord(this, index)
        : throw new ArgumentOutOfRangeException(nameof(index));

    internal bool IsGemUsed(int index, EquipmentRecord? excluded = null)
        => UsedGemIndices(excluded).Contains(index);

    internal HashSet<EquipmentSlot> GemEquipmentSlots(int index)
    {
        var slots = new HashSet<EquipmentSlot>();
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
            for (int item = 0; item < 500; item++)
            {
                int offset = EquipmentCatalog.InventoryOffset(slot) + item * EquipmentRecord.InventorySize;
                if (ReadByte(offset + 0x10) == 0) continue;
                for (int socket = 0; socket < 3; socket++)
                {
                    int reference = offset + 0x18 + socket * 8;
                    if (ReadUInt16(reference + 2) == 3 && ReadUInt16(reference) == index) slots.Add(slot);
                }
            }
        return slots;
    }

    internal HashSet<int> UsedGemIndices(EquipmentRecord? excluded = null)
    {
        var used = new HashSet<int>();
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
            for (int item = 0; item < 500; item++)
            {
                int offset = EquipmentCatalog.InventoryOffset(slot) + item * EquipmentRecord.InventorySize;
                if (ReadByte(offset + 0x10) == 0 || excluded is not null && excluded.Slot == slot && excluded.Index == item) continue;
                for (int socket = 0; socket < 3; socket++)
                {
                    int reference = offset + 0x18 + socket * 8;
                    if (ReadUInt16(reference + 2) == 3) used.Add(ReadUInt16(reference));
                }
            }
        return used;
    }

    public void MaxAllAP()
    {
        foreach (var character in Characters)
            character.SetResources(ap: CharacterRecord.MaximumAP);
    }

    public bool CanLearnAndMaxAllArts => Campaign != Campaign.Unknown
        && Characters.Count == PartyIds.Count && Characters.All(character => character.CanEditProgression);

    public void LearnAndMaxAllArts()
    {
        if (!CanLearnAndMaxAllArts)
            throw new ArgumentException("Arts require an identified campaign and supported characters.");
        foreach (var character in Characters) character.LearnAndMaxArts();
    }

    public void MaxAllSkills()
    {
        if (Campaign != Campaign.MainStory || Characters.Any(character => character.Id is < 1 or > 8))
            throw new ArgumentException("Skill learning requires an identified main-story party.");
        foreach (var character in Characters) character.MaxSkills();
    }

    internal byte ReadByte(int offset) => _data[offset];
    internal bool IsSkillTreeUnlocked(int flag)
    {
        if (flag is < 1 or > 14)
            throw new ArgumentOutOfRangeException(nameof(flag));
        int bit = 0x2cdd + flag;
        return (ReadByte(0x50 + (bit >> 3)) & (1 << (bit & 7))) != 0;
    }
    internal void WriteByte(int offset, byte value) => _data[offset] = value;
    internal void WriteUInt16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(offset, 2), value);
    internal uint ReadUInt32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset, 4));
    internal ushort ReadUInt16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(offset, 2));
    internal void WriteUInt32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(_data.AsSpan(offset, 4), value);

    private void ValidateLayout()
    {
        int count = _data[PartyOffset + 24];
        if (count is < 1 or > 12)
            throw new InvalidDataException("Unrecognized party layout. This save cannot be edited safely.");
        var ids = PartyIds;
        if (ids.Any(id => id is < 1 or > 27) || ids.Distinct().Count() != count)
            throw new InvalidDataException("Unrecognized character IDs in the saved party.");
        foreach (int id in ids.Where(id => id <= 15))
        {
            uint level = ReadUInt32(CharacterOffset + (id - 1) * CharacterSize);
            if (level is < 1 or > 99)
                throw new InvalidDataException($"Character {id} has an unsupported level or record layout.");
        }
    }
}
