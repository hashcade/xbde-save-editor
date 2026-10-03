namespace XbdeEditor.Core;

public sealed class SkillTreeRecord
{
    public const uint MaximumStoredSP = 999_999;
    private readonly SaveDocument _document;
    private readonly SkillTreeDefinition _definition;
    private int CharacterOffset => SaveDocument.CharacterOffset + (_definition.CharacterId - 1) * SaveDocument.CharacterSize;
    private int ProgressOffset => CharacterOffset + 0x7c + (Index - 1) * 4;
    private int CountOffset => CharacterOffset + 0x90 + (Index - 1) * 4;

    internal SkillTreeRecord(SaveDocument document, SkillTreeDefinition definition)
    {
        _document = document;
        _definition = definition;
    }

    public int Index => _definition.Index;
    public string Name => _definition.Name;
    public IReadOnlyList<SkillDefinition> Skills => _definition.Skills;
    public uint Progress => _document.ReadUInt32(ProgressOffset);
    public int LearnedCount => unchecked((int)_document.ReadUInt32(CountOffset));
    public int MinimumLearnedCount => Skills.TakeWhile(skill => skill.RequiredSP == 0).Count();
    public bool IsUnlocked => _definition.UnlockFlag == 0 || _document.IsSkillTreeUnlocked(_definition.UnlockFlag);
    public bool CanEdit => _document.CanEditSkills && IsUnlocked;
    public bool CanUnlock => _document.CanEditSkills && _definition.UnlockFlag != 0 && !IsUnlocked;
    public uint MaximumProgress => LearnedCount is >= 0 and < 5 && Skills[LearnedCount].RequiredSP > 0
        ? Skills[LearnedCount].RequiredSP - 1 : 0;

    public void SetProgress(uint progress)
    {
        EnsureEditable();
        if (LearnedCount < MinimumLearnedCount || LearnedCount >= 5)
            throw new ArgumentException("Only an incomplete skill tree can receive remaining SP.");
        if (progress > MaximumProgress)
            throw new ArgumentOutOfRangeException(nameof(progress), $"Remaining SP must be between 0 and {MaximumProgress}.");
        _document.WriteUInt32(ProgressOffset, progress);
    }

    public void SetLearnedCount(int count)
    {
        EnsureEditable();
        if (count < MinimumLearnedCount || count > 5)
            throw new ArgumentOutOfRangeException(nameof(count), $"Skills learned must be between {MinimumLearnedCount} and 5.");
        _document.WriteUInt32(CountOffset, (uint)count);
        _document.WriteUInt32(ProgressOffset, 0);
    }

    public void Maximize() => SetLearnedCount(5);

    public void Unlock()
    {
        if (!_document.CanEditSkills)
            throw new ArgumentException("Skill branches require an identified main-story save with format version 7.");
        if (_definition.UnlockFlag != 0) _document.UnlockSkillTree(_definition.UnlockFlag);
    }

    private void EnsureEditable()
    {
        if (!CanEdit) throw new ArgumentException("Only unlocked skill trees in an identified main-story save can be edited.");
    }
}
