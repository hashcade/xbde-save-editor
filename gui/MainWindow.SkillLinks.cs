using Avalonia.Controls;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record SkillLinkChoice(int Id, string Label);
    private sealed record SkillLinkSlotRow(SkillLinkRecord Link, string Label, string Value,
        SkillLinkChoice[] Choices, SkillLinkChoice? Selected)
    {
        public bool CanEdit => Link.CanEdit;
        public bool ReadOnly => !CanEdit;
    }
    private sealed record SkillLinkGroupRow(string Name, SkillLinkSlotRow[] Slots);
    private bool _refreshingSkillLinks;

    public void ShowSkillLinks()
    {
        ShowCharacters();
        CharacterNavigation.SelectedIndex = 4;
    }

    private void RefreshSkillLinks()
    {
        if (SkillLinkList is null) return;
        _refreshingSkillLinks = true;
        try
        {
            var links = _character?.SkillLinks ?? [];
            SkillLinkList.ItemsSource = links.GroupBy(link => link.SourceCharacterId).Select(group =>
                new SkillLinkGroupRow(CharacterCatalog.Get(group.Key, UiLanguage.Current), group.Select(SkillLinkRow).ToArray())).ToArray();
            SkillLinkCoinValue.Text = _character is null ? null : string.Format(UiLanguage.Get("SkillLinkCoins"),
                _character.LinkedSkillCoinCost?.ToString() ?? "—", _character.AffinityCoins);
            SkillLinksUnavailableValue.IsVisible = links.Count == 0 || Session?.Document.CanEditSkillLinks != true;
        }
        finally { _refreshingSkillLinks = false; }
    }

    private static SkillLinkSlotRow SkillLinkRow(SkillLinkRecord link)
    {
        string label = $"{link.Index} · {UiLanguage.Get("SkillShape" + link.Shape)}";
        string value = link.SkillId == 0 ? UiLanguage.Get("None") : link.Skill?.Name ?? $"#{link.SkillId}";
        if (!link.IsUnlocked) value += $" · {UiLanguage.Get("Locked")}";
        var choices = link.Choices.Select(skill => new SkillLinkChoice(skill.Id,
            $"{skill.Name} · {string.Format(UiLanguage.Get("SkillLinkCost"), skill.AffinityCoins)}"))
            .Prepend(new SkillLinkChoice(0, UiLanguage.Get("None"))).ToList();
        // Preserve unresolved or no-longer-learned assignments for inspection; opening never edits them.
        if (link.SkillId != 0 && choices.All(choice => choice.Id != link.SkillId))
            choices.Add(new(link.SkillId, value));
        return new(link, label, value, choices.ToArray(), choices.FirstOrDefault(choice => choice.Id == link.SkillId));
    }

    private void SkillLink_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingSkillLinks || sender is not ComboBox { Tag: SkillLinkSlotRow row, SelectedItem: SkillLinkChoice choice }
            || choice.Id == row.Link.SkillId) return;
        if (!CommitProgressionDraft())
        {
            RefreshSkillLinks();
            return;
        }
        try
        {
            row.Link.SetSkill(choice.Id);
            ShowStatus(null);
        }
        catch (ArgumentException) { ShowStatus(UiLanguage.Get("InvalidSkillLink")); }
        RefreshCharacters();
    }
}
