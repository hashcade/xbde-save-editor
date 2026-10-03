namespace XbdeEditor.Core;

public sealed class ArtRecord
{
    private readonly SaveDocument _document;
    private readonly ArtDefinition _definition;
    private int Offset => SaveDocument.ArtsOffset + (Id - 1) * 2;

    internal ArtRecord(SaveDocument document, ArtDefinition definition)
    {
        _document = document;
        _definition = definition;
    }

    public int Id => _definition.Id;
    public string Name => _definition.IsMonado ? $"Monado {_definition.Name}" : _definition.Name;
    public int Level => _document.ReadByte(Offset);
    public byte ManualFlags => _document.ReadByte(Offset + 1);
    public bool Learned => Level != 0;
    public bool IsTalent => _definition.IsTalent;
    public bool IsLevelLearned => _definition.IsLevelLearned;
    public bool RequiresEvent => _definition.LearnType == 2;
    public int LearnLevel => _definition.LearnLevel;
    public bool CanLearn => !Learned && (IsLevelLearned || CanLearnEvent)
        && _document.GetCharacter(_definition.CharacterId).CanEditProgression;
    private bool CanLearnEvent => (Id is 118 or 143) && _document.CanLearnEventArts;
    public bool CanEdit => Learned && !IsTalent && _document.GetCharacter(_definition.CharacterId).CanEditProgression
        && !(_document.Campaign == Campaign.FutureConnected && Id == 4);
    private int ManualLevelLimit => (ManualFlags & 7) switch
    {
        7 => 12, 3 => 10, 1 or 5 => 7, _ => 4
    };
    public int MaximumLevel
    {
        get
        {
            if (IsTalent) return 1;
            if (_definition.IsMonado) return Math.Min(10, ManualLevelLimit);
            return _document.Campaign == Campaign.MainStory && _definition.HasMasterBook ? 12 : 10;
        }
    }
    public int UnlockedMaximum => Math.Min(MaximumLevel, ManualLevelLimit);

    public void SetLevel(int level)
    {
        if (!CanEdit) throw new ArgumentException("Only learned, upgradeable arts in an identified campaign can be edited.");
        ValidateLevel(level);
        WriteLevel(level);
    }

    public void Learn(int level = 1)
    {
        if (!CanLearn) throw new ArgumentException("Only supported unlearned arts can be learned directly.");
        ValidateLevel(level);
        WriteLevel(level);
    }

    private void ValidateLevel(int level)
    {
        if (level < 1 || level > MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(level), $"Art level must be between 1 and {MaximumLevel}.");
    }

    private void WriteLevel(int level)
    {
        _document.WriteByte(Offset, (byte)level);
        if (!_definition.IsMonado)
        {
            byte required = level switch { > 10 => 7, > 7 => 3, > 4 => 1, _ => 0 };
            _document.WriteByte(Offset + 1, (byte)(ManualFlags | required));
        }
        // Native Melia initialization copies summon levels to their discharge records.
        foreach (var discharge in ArtCatalog.All.Where(art => art.LinkedArtId == Id))
            _document.WriteByte(SaveDocument.ArtsOffset + (discharge.Id - 1) * 2, (byte)level);
    }
}
