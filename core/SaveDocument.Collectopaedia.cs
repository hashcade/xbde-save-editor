namespace XbdeEditor.Core;

public sealed partial class SaveDocument
{
    public bool CanInspectCollectopaedia => Campaign != Campaign.Unknown && ReadUInt32(0) == 7;

    public IReadOnlyList<CollectopaediaRecord> Collectopaedia => CanInspectCollectopaedia
        ? CollectopaediaCatalog.All.Where(entry => entry.Campaign == Campaign)
            .Select(entry => new CollectopaediaRecord(this, entry)).ToArray() : [];

    public CollectopaediaRecord GetCollectopaediaEntry(int id) => Collectopaedia.FirstOrDefault(entry => entry.Id == id)
        ?? throw new ArgumentException("Choose a Collectopaedia entry from this campaign and save format 7.", nameof(id));
}
