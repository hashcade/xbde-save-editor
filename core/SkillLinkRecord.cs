namespace XbdeEditor.Core;

public sealed class SkillLinkRecord
{
    private readonly SaveDocument _document;
    private int CharacterOffset => SaveDocument.CharacterOffset + (CharacterId - 1) * SaveDocument.CharacterSize;
    private int Offset => CharacterOffset + 0xc4 + (SourceCharacterId - 1) * 5 + Index - 1;

    internal SkillLinkRecord(SaveDocument document, int characterId, int sourceCharacterId, int index)
    {
        _document = document;
        CharacterId = characterId;
        SourceCharacterId = sourceCharacterId;
        Index = index;
    }

    public int CharacterId { get; }
    public int SourceCharacterId { get; }
    public int Index { get; }
    public SkillShape Shape => SkillLinkCatalog.GetShape(CharacterId, SourceCharacterId, Index);
    public int SkillId => _document.ReadByte(Offset);
    public SkillDefinition? Skill => SkillCatalog.Find(SkillId);
    public uint HighestUnlockedSlot => _document.ReadUInt32(CharacterOffset + 0xa4 + (SourceCharacterId - 1) * 4);
    public bool IsUnlocked => HighestUnlockedSlot <= 4 && Index - 1 <= HighestUnlockedSlot;
    public bool CanEdit => _document.CanEditSkillLinks && IsUnlocked;
    public IReadOnlyList<SkillDefinition> Choices => !CanEdit ? [] : _document.GetCharacter(SourceCharacterId).SkillTrees
        .Where(tree => tree.IsUnlocked && tree.LearnedCount is >= 0 and <= 5)
        .SelectMany(tree => tree.Skills.Take(tree.LearnedCount))
        .Where(skill => skill.Shape == Shape && CanSetSkill(skill.Id)).ToArray();

    public bool CanSetSkill(int skillId)
    {
        try { ValidateSkill(skillId); return true; }
        catch (ArgumentException) { return false; }
    }

    public void SetSkill(int skillId)
    {
        ValidateSkill(skillId);
        _document.WriteByte(Offset, (byte)skillId);
    }

    private void ValidateSkill(int skillId)
    {
        if (!CanEdit) throw new ArgumentException("Only unlocked skill links in a supported main-story save can be edited.");
        if (skillId == 0) return;
        var skill = SkillCatalog.Find(skillId) ?? throw new ArgumentException("Unrecognized skill.", nameof(skillId));
        if (skill.CharacterId != SourceCharacterId || skill.Shape != Shape)
            throw new ArgumentException("The skill must belong to the source character and match the slot shape.");
        var tree = _document.GetCharacter(SourceCharacterId).GetSkillTree(skill.TreeIndex);
        if (!tree.IsUnlocked || tree.LearnedCount is < 0 or > 5 || tree.LearnedCount <= skill.NodeIndex)
            throw new ArgumentException("The source character must have learned this skill.");
        var character = _document.GetCharacter(CharacterId);
        if (character.SkillLinks.Any(link => link.SourceCharacterId == SourceCharacterId
            && link.Index != Index && link.SkillId == skillId))
            throw new ArgumentException("A skill cannot occupy two slots from the same source character.");
        if (!character.TryGetLinkedSkillCost(out int used))
            throw new ArgumentException("Remove unrecognized existing skill links before assigning another skill.");
        int previousCost = Skill?.AffinityCoins ?? 0;
        if (used - previousCost + skill.AffinityCoins > Math.Max(character.AffinityCoins, (uint)used))
            throw new ArgumentException("Not enough available Affinity Coins for this skill link.");
    }
}
