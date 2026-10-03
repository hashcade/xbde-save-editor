using System.Buffers.Binary;
using System.Security.Cryptography;

namespace XbdeEditor.Core;

public enum Campaign { MainStory, FutureConnected, Unknown }

public sealed class SaveDocument
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
    public IReadOnlyList<GemRecord> Gems => Enumerable.Range(0, 500).Select(index => new GemRecord(this, index))
        .Where(gem => gem.Exists).ToArray();
    public GemRecord GetGem(int index) => index is >= 0 and < 500 ? new GemRecord(this, index)
        : throw new ArgumentOutOfRangeException(nameof(index));

    internal bool IsGemUsed(int index, EquipmentRecord? excluded = null)
        => UsedGemIndices(excluded).Contains(index);

    internal HashSet<int> UsedGemIndices(EquipmentRecord? excluded = null)
    {
        var used = new HashSet<int>();
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
            for (int item = 0; item < 500; item++)
            {
                int offset = EquipmentCatalog.InventoryOffset(slot) + item * EquipmentRecord.InventorySize;
                if (ReadByte(offset + 0x10) != 1 || excluded is not null && excluded.Slot == slot && excluded.Index == item) continue;
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
