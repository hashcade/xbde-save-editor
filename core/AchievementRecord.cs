namespace XbdeEditor.Core;

public sealed class AchievementRecord
{
    private readonly SaveDocument _save;
    public AchievementDefinition Definition { get; }
    internal AchievementRecord(SaveDocument save, AchievementDefinition definition)
    {
        _save = save;
        Definition = definition;
    }

    public int Id => Definition.Id;
    private int Bit => 0x2838 + Id % 200;
    private int FlagOffset => 0x50 + (Bit >> 3);
    private int CounterOffset => 0xe30 + 2 * (Id % 200);
    public bool Completed => (_save.ReadByte(FlagOffset) & (1 << (Bit & 7))) != 0;
    public int? Progress => Definition.HasCounter ? _save.ReadUInt16(CounterOffset) : null;
    public bool? CounterMeetsRequirement
    {
        get
        {
            if (Progress is not int progress) return null;
            return Definition.ConditionType == 3 ? progress == Definition.Required : progress >= Definition.Required;
        }
    }
    public bool HasUnmetCompletedCounter => Completed && CounterMeetsRequirement == false;
    public bool CanUnlock => _save.CanEditAchievements && !Completed;
    public bool CanRepairCounter => _save.CanEditAchievements && HasUnmetCompletedCounter;

    public void RepairCounter()
    {
        if (!_save.CanEditAchievements)
            throw new ArgumentException("Achievement counter repair requires a confirmed main-story save with format version 7.");
        if (!HasUnmetCompletedCounter) return;
        _save.WriteUInt16(CounterOffset, checked((ushort)Definition.Required));
    }

    public void Unlock()
    {
        if (!_save.CanEditAchievements)
            throw new ArgumentException("Achievements require a confirmed main-story save with format version 7.");
        if (Completed) return;
        if (Definition.HasCounter)
        {
            int count = Definition.Required;
            if (Definition.ConditionType == 4) count = Math.Max(count, Progress!.Value);
            _save.WriteUInt16(CounterOffset, checked((ushort)count));
        }
        _save.WriteByte(FlagOffset, (byte)(_save.ReadByte(FlagOffset) | (1 << (Bit & 7))));
    }
}
