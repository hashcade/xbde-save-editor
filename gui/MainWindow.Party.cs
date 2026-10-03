using Avalonia.Controls;
using Avalonia.Interactivity;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record PartyRow(int Id, string Label, bool CanMoveUp, bool CanMoveDown);

    private void RefreshParty()
    {
        if (PartyOrderList is null) return;
        var ids = Session?.Document.PartyIds ?? [];
        bool editable = Session?.Document.CanReorderParty == true;
        PartyOrderList.ItemsSource = ids.Select((id, index) => new PartyRow(id,
            CharacterCatalog.Get(id, UiLanguage.Current), editable && index > 0, editable && index < ids.Count - 1)).ToArray();
    }

    private void MovePartyUp_Click(object? sender, RoutedEventArgs e) => MovePartyMember(sender, -1);
    private void MovePartyDown_Click(object? sender, RoutedEventArgs e) => MovePartyMember(sender, 1);

    private void MovePartyMember(object? sender, int direction)
    {
        if (Session is null || sender is not Button { Tag: int id }
            || !CommitProgressionDraft() || !CommitSkillDrafts() || !CommitGemDraft() || !CommitInventoryDraft()) return;
        var ids = Session.Document.PartyIds.ToArray();
        int index = Array.IndexOf(ids, id);
        int target = index + direction;
        if (index < 0 || target < 0 || target >= ids.Length) return;
        (ids[index], ids[target]) = (ids[target], ids[index]);
        try
        {
            Session.Document.ReorderParty(ids);
            RefreshParty();
            RefreshCharacters();
            ShowStatus(null);
        }
        catch (ArgumentException) { ShowStatus(UiLanguage.Get("InvalidValue")); }
    }
}
