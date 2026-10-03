namespace XbdeEditor.Core;

public sealed class CharacterRecord
{
    public const uint MaximumAP = 99_999_999;
    public const uint MaximumReserveExperience = 199_999_998;
    private readonly SaveDocument _document;
    private readonly int _offset;

    internal CharacterRecord(SaveDocument document, int id)
    {
        _document = document;
        Id = id;
        _offset = SaveDocument.CharacterOffset + (id - 1) * SaveDocument.CharacterSize;
    }

    public int Id { get; }
    public uint Level => _document.ReadUInt32(_offset);
    public uint Experience => _document.ReadUInt32(_offset + 4);
    public uint AP => _document.ReadUInt32(_offset + 8);
    public uint AffinityCoins => _document.ReadUInt32(_offset + 12);
    public uint ExpertLevel => _document.ReadUInt32(_offset + 0xec);
    public uint ExpertExperience => _document.ReadUInt32(_offset + 0xf0);
    public uint ReserveExperience => _document.ReadUInt32(_offset + 0xf4);
    public bool UsesAffinityCoins => _document.Campaign == Campaign.MainStory;
    public bool CanEditProgression => _document.Campaign switch
    {
        Campaign.MainStory => Id is >= 1 and <= 8,
        Campaign.FutureConnected => Id is 1 or 7 or 14 or 15,
        _ => false
    };
    public uint MinimumLevel => LevelProgression.MinimumLevel(Id, _document.Campaign);
    public IReadOnlyList<EquipmentRecord> Equipment => Enum.GetValues<EquipmentSlot>()
        .Select(slot => new EquipmentRecord(_document, Id, slot)).ToArray();
    public EquipmentRecord GetEquipment(EquipmentSlot slot) => Equipment.FirstOrDefault(equipment => equipment.Slot == slot)
        ?? throw new ArgumentOutOfRangeException(nameof(slot));
    public IReadOnlyList<ArtRecord> Arts => ArtCatalog.All.Where(art => art.CharacterId == Id && art.LinkedArtId is null)
        .OrderBy(art => art.Order).ThenBy(art => art.Id)
        .Select(art => new ArtRecord(_document, art)).ToArray();

    public ArtRecord GetArt(int id) => Arts.FirstOrDefault(art => art.Id == id)
        ?? throw new ArgumentException("This art does not belong to the selected character.", nameof(id));

    public IReadOnlyList<SkillTreeRecord> SkillTrees => SkillCatalog.All.Where(tree => tree.CharacterId == Id)
        .OrderBy(tree => tree.Index).Select(tree => new SkillTreeRecord(_document, tree)).ToArray();

    public SkillTreeRecord GetSkillTree(int index) => SkillTrees.FirstOrDefault(tree => tree.Index == index)
        ?? throw new ArgumentException("This skill tree does not belong to the selected character.", nameof(index));

    public IReadOnlyList<SkillLinkRecord> SkillLinks => _document.Campaign != Campaign.MainStory || Id is < 1 or > 8 ? []
        : _document.PartyIds.Where(source => _document.IsSkillLinkSourceAvailable(source) && source != Id
            && !(Id is 3 or 8 && source is 3 or 8))
            .Order().SelectMany(source => Enumerable.Range(1, 5)
                .Select(index => new SkillLinkRecord(_document, Id, source, index))).ToArray();

    public SkillLinkRecord GetSkillLink(int sourceCharacterId, int index) => SkillLinks
        .FirstOrDefault(link => link.SourceCharacterId == sourceCharacterId && link.Index == index)
        ?? throw new ArgumentException("This source character or skill-link slot is not available.");

    public int? LinkedSkillCoinCost => TryGetLinkedSkillCost(out int cost) ? cost : null;

    internal bool TryGetLinkedSkillCost(out int cost)
    {
        cost = 0;
        if (_document.Campaign != Campaign.MainStory || Id is < 1 or > 8) return false;
        var activeLinks = Enumerable.Range(1, 8).Where(source => _document.IsSkillLinkSourceAvailable(source) && source != Id
            && !(Id is 3 or 8 && source is 3 or 8)).SelectMany(source => Enumerable.Range(1, 5)
                .Select(index => new SkillLinkRecord(_document, Id, source, index)));
        foreach (var link in activeLinks)
        {
            if (link.HighestUnlockedSlot > 4) return false;
            if (!link.IsUnlocked || link.SkillId == 0) continue;
            if (link.Skill is not { } skill) return false;
            cost += skill.AffinityCoins;
        }
        return true;
    }

    public void MaxSkills()
    {
        if (!CanUnlockAndMaxSkills)
            throw new ArgumentException("Skill learning requires an identified main-story character.");
        foreach (var tree in SkillTrees.Where(tree => tree.CanEdit)) tree.Maximize();
    }

    public bool CanUnlockAndMaxSkills => _document.CanEditSkills && Id is >= 1 and <= 8;

    public void UnlockAndMaxSkills()
    {
        if (!CanUnlockAndMaxSkills)
            throw new ArgumentException("Skill unlocking requires a supported main-story character and save format 7.");
        foreach (var tree in SkillTrees)
        {
            tree.Unlock();
            tree.Maximize();
        }
    }

    public void MaxArts()
    {
        if (!CanEditProgression) throw new ArgumentException("Arts require an identified campaign and supported character.");
        foreach (var art in Arts.Where(art => art.CanEdit)) art.SetLevel(art.MaximumLevel);
    }

    public void LearnAndMaxArts()
    {
        if (!CanEditProgression) throw new ArgumentException("Arts require an identified campaign and supported character.");
        foreach (var art in Arts.Where(art => art.CanLearn)) art.Learn(art.MaximumLevel);
        MaxArts();
    }

    public void SetProgression(uint? level = null, uint? experience = null)
    {
        uint targetLevel = level ?? Level;
        if (targetLevel < MinimumLevel || targetLevel > LevelProgression.MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(level), $"Level must be between {MinimumLevel} and 99.");
        if (level is null && experience is null) return;
        uint targetExperience = experience ?? (targetLevel == Level ? Experience : 0);
        var normalized = LevelProgression.Normalize(targetLevel, targetExperience);
        if (normalized.Level != Level)
        {
            // Loading rebuilds stats from level. Preserve the highest attained level,
            // as the native level rebuild does, and reset its EXP accumulator.
            _document.WriteUInt32(_offset + 0xec, Math.Max(ExpertLevel, normalized.Level));
            _document.WriteUInt32(_offset + 0xf0, 0);
        }
        _document.WriteUInt32(_offset, normalized.Level);
        _document.WriteUInt32(_offset + 4, normalized.Experience);
    }

    public void SetResources(uint? ap = null, uint? affinityCoins = null, uint? reserveExperience = null)
    {
        if (ap is > MaximumAP)
            throw new ArgumentOutOfRangeException(nameof(ap), "AP must be between 0 and 99,999,999.");
        if (affinityCoins is > 999)
            throw new ArgumentOutOfRangeException(nameof(affinityCoins), "Affinity Coins must be between 0 and 999.");
        if (affinityCoins is not null && !UsesAffinityCoins)
            throw new ArgumentException("Affinity Coins can only be edited in an identified main-story save.");
        if (reserveExperience is > MaximumReserveExperience)
            throw new ArgumentOutOfRangeException(nameof(reserveExperience), "Reserve EXP must be between 0 and 199,999,998.");
        if (ap is { } newAP) _document.WriteUInt32(_offset + 8, newAP);
        if (affinityCoins is { } newCoins) _document.WriteUInt32(_offset + 12, newCoins);
        if (reserveExperience is { } newReserve) _document.WriteUInt32(_offset + 0xf4, newReserve);
    }
}
