using Avalonia.Controls;
using Avalonia.Interactivity;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record ArtRow(ArtRecord Art, string Label)
    {
        public int Id => Art.Id;
    }
    private ArtRecord? _art;
    private bool _refreshingArts;

    public void ShowArts()
    {
        ShowCharacters();
        CharacterNavigation.SelectedIndex = 1;
    }

    private void CharacterNavigation_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (GeneralCharacterScroll is null || ArtsPanel is null || SkillsPanel is null || MaxAllSkillsButton is null
            || LearnMaxAllArtsButton is null || EquipmentPanel is null) return;
        GeneralCharacterScroll.IsVisible = CharacterNavigation.SelectedIndex == 0;
        ArtsPanel.IsVisible = LearnMaxAllArtsButton.IsVisible = CharacterNavigation.SelectedIndex == 1;
        SkillsPanel.IsVisible = MaxAllSkillsButton.IsVisible = CharacterNavigation.SelectedIndex == 2;
        EquipmentPanel.IsVisible = CharacterNavigation.SelectedIndex == 3;
    }

    private void RefreshArts()
    {
        if (ArtList is null) return;
        _refreshingArts = true;
        try
        {
            int? selected = _art?.Id;
            string query = ArtSearch.Text?.Trim() ?? "";
            var arts = _character?.Arts ?? [];
            var rows = arts.Select(art => new ArtRow(art,
                art.Learned ? $"{art.Name} · Lv. {art.Level}" : art.Name))
                .Where(row => row.Label.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            ArtList.ItemsSource = rows;
            ArtList.SelectedItem = rows.FirstOrDefault(row => row.Id == selected)
                ?? rows.FirstOrDefault(row => row.Art.CanEdit) ?? rows.FirstOrDefault();
            _art = (ArtList.SelectedItem as ArtRow)?.Art;
            ArtInputs.IsEnabled = _art is not null;
            ArtLevelInput.IsEnabled = MaxArtButton.IsEnabled = _art?.CanEdit ?? false;
            MaxArtButton.IsVisible = _art?.CanEdit ?? false;
            LearnArtButton.IsVisible = LearnArtButton.IsEnabled = _art?.CanLearn ?? false;
            ArtLevelInput.IsVisible = _art?.CanEdit ?? false;
            ArtLevelValue.IsVisible = !ArtLevelInput.IsVisible;
            ArtLevelValue.Text = _art is { Learned: true } ? _art.Level.ToString() : "—";
            MaxCharacterArtsButton.IsEnabled = arts.Any(art => art.CanEdit || art.CanLearn);
            var characters = Session?.Document.Characters ?? [];
            LearnMaxAllArtsButton.IsEnabled = Session?.Document.CanLearnAndMaxAllArts == true
                && characters.Any(character => character.Arts.Any(art => art.CanEdit || art.CanLearn));
            ArtStatusValue.Text = _art is null ? null : UiLanguage.Get(ArtState(_art));
            ArtMaximumValue.Text = _art?.MaximumLevel.ToString();
            ArtLevelInput.ItemsSource = Enumerable.Range(1, _art?.MaximumLevel ?? 1).ToArray();
            // Existing out-of-range levels are inspectable, never silently normalized.
            if (_art is { Level: > 0 } && !ArtLevelInput.Items.OfType<int>().Contains(_art.Level))
                ArtLevelInput.ItemsSource = ArtLevelInput.Items.OfType<int>().Append(_art.Level).ToArray();
            ArtLevelInput.SelectedItem = _art is { Learned: true } ? _art.Level : null;
        }
        finally { _refreshingArts = false; }
    }

    private static string ArtState(ArtRecord art)
    {
        if (!art.Learned) return art.RequiresEvent ? "ArtEventLocked" : "NotLearned";
        if (art.IsTalent) return "FixedTalent";
        return art.CanEdit ? "Upgradeable" : "Unavailable";
    }

    private void ArtSearch_Changed(object? sender, TextChangedEventArgs e) => RefreshArts();
    private void ArtList_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingArts) return;
        _art = (ArtList.SelectedItem as ArtRow)?.Art;
        RefreshArts();
    }

    private void ArtLevelInput_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingArts || _art is null || ArtLevelInput.SelectedItem is not int level || level == _art.Level) return;
        EditArts(() => _art.SetLevel(level));
    }

    private void MaxArt_Click(object? sender, RoutedEventArgs e)
    {
        if (_art is not null) EditArts(() => _art.SetLevel(_art.MaximumLevel));
    }

    private void MaxCharacterArts_Click(object? sender, RoutedEventArgs e)
    {
        if (_character is not null) EditArts(_character.LearnAndMaxArts);
    }

    private void LearnArt_Click(object? sender, RoutedEventArgs e)
    {
        if (_art is not null) EditArts(() => _art.Learn());
    }

    private void LearnMaxAllArts_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is { } session) EditArts(session.Document.LearnAndMaxAllArts);
    }

    private void EditArts(Action edit)
    {
        if (!CommitProgressionDraft())
        {
            RefreshArts();
            return;
        }
        try
        {
            edit();
            ShowStatus(null);
        }
        catch (ArgumentException error) { ShowStatus(error.Message); }
        RefreshCharacters();
    }
}
