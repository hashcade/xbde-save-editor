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
    public IReadOnlyList<ArtRecord> Arts => ArtCatalog.All.Where(art => art.CharacterId == Id && art.LinkedArtId is null)
        .OrderBy(art => art.Order).ThenBy(art => art.Id)
        .Select(art => new ArtRecord(_document, art)).ToArray();

    public ArtRecord GetArt(int id) => Arts.FirstOrDefault(art => art.Id == id)
        ?? throw new ArgumentException("This art does not belong to the selected character.", nameof(id));

    public void MaxArts()
    {
        if (!CanEditProgression) throw new ArgumentException("Arts require an identified campaign and supported character.");
        foreach (var art in Arts.Where(art => art.CanEdit)) art.SetLevel(art.MaximumLevel);
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
