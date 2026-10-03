using Avalonia.Controls;
using Avalonia.Interactivity;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record CharacterRow(int Id, string Label);
    private CharacterRecord? _character;
    private int? _selectedCharacter;
    private bool _refreshingCharacters;

    private void Navigation_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (MainScroll is null || CharactersPanel is null || ItemsPanel is null || AffinityPanel is null || AchievementsPanel is null) return;
        MainScroll.IsVisible = MainNavigation.SelectedIndex == 0;
        CharactersPanel.IsVisible = MainNavigation.SelectedIndex == 1;
        ItemsPanel.IsVisible = MainNavigation.SelectedIndex == 2;
        AffinityPanel.IsVisible = MainNavigation.SelectedIndex == 3;
        AchievementsPanel.IsVisible = MainNavigation.SelectedIndex == 4;
    }

    public void ShowCharacters() => MainNavigation.SelectedIndex = 1;

    private void RefreshCharacters()
    {
        if (CharacterList is null) return;
        _refreshingCharacters = true;
        try
        {
            string query = CharacterSearch.Text?.Trim() ?? "";
            var rows = Session?.Document.Characters.Select(character => new CharacterRow(character.Id,
                $"{CharacterCatalog.Get(character.Id, UiLanguage.Current)} · Lv. {character.Level}"))
                .Where(row => row.Label.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];
            CharacterList.ItemsSource = rows;
            CharacterList.SelectedItem = rows.FirstOrDefault(row => row.Id == _selectedCharacter) ?? rows.FirstOrDefault();
            _selectedCharacter = (CharacterList.SelectedItem as CharacterRow)?.Id;
            _character = _selectedCharacter is { } id ? Session?.Document.GetCharacter(id) : null;
            CharacterInputs.IsEnabled = _character is not null;
            MaxAllAPButton.IsEnabled = Session is not null;
            bool canEditProgression = _character?.CanEditProgression ?? false;
            ProgressionInputs.IsEnabled = canEditProgression;
            uint minimum = canEditProgression ? _character!.MinimumLevel : 1;
            LevelInput.ItemsSource = Enumerable.Range((int)minimum, (int)(100 - minimum)).Select(level => (uint)level).ToArray();
            LevelInput.SelectedItem = _character?.Level;
            ExperienceInput.Maximum = Math.Max(LevelProgression.MaximumExperience, _character?.Experience ?? 0);
            ExperienceInput.Value = _character?.Experience;
            ExpertLevelValue.Text = _character?.ExpertLevel.ToString();
            ExpertExperienceValue.Text = _character?.ExpertExperience.ToString();
            APInput.Maximum = Math.Max(CharacterRecord.MaximumAP, _character?.AP ?? 0);
            APInput.Value = _character?.AP;
            ReserveExperienceInput.Maximum = Math.Max(CharacterRecord.MaximumReserveExperience, _character?.ReserveExperience ?? 0);
            ReserveExperienceInput.Value = _character?.ReserveExperience;
            AffinityCoinsField.IsVisible = _character?.UsesAffinityCoins ?? true;
            AffinityCoinsInput.Maximum = Math.Max(999, _character?.AffinityCoins ?? 0);
            AffinityCoinsInput.Value = _character?.AffinityCoins;
            RefreshArts();
            RefreshSkills();
            RefreshSkillLinks();
            RefreshEquipment();
        }
        finally { _refreshingCharacters = false; }
    }

    private void CharacterList_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingCharacters) return;
        if (!CommitProgressionDraft())
        {
            _refreshingCharacters = true;
            CharacterList.SelectedItem = CharacterList.Items.OfType<CharacterRow>().FirstOrDefault(row => row.Id == _selectedCharacter);
            _refreshingCharacters = false;
            return;
        }
        _selectedCharacter = (CharacterList.SelectedItem as CharacterRow)?.Id;
        RefreshCharacters();
    }

    private void CharacterSearch_Changed(object? sender, TextChangedEventArgs e)
    {
        if (CommitProgressionDraft()) RefreshCharacters();
    }

    private bool CharacterValuesValid()
    {
        if (_character is null) return true;
        if (_character.CanEditProgression && (LevelInput.SelectedItem is not uint level
            || level < _character.MinimumLevel || level > LevelProgression.MaximumLevel
            || !ValidResource(ExperienceInput.Value, LevelProgression.MaximumExperience, _character.Experience))) return false;
        if (!ValidResource(APInput.Value, CharacterRecord.MaximumAP, _character.AP)) return false;
        if (!ValidResource(ReserveExperienceInput.Value, CharacterRecord.MaximumReserveExperience, _character.ReserveExperience)) return false;
        if (!_character.UsesAffinityCoins) return true;
        return ValidResource(AffinityCoinsInput.Value, 999, _character.AffinityCoins);
    }

    private static bool ValidResource(decimal? value, uint maximum, uint original) =>
        WholeNumber(value, out uint number) && (number <= maximum || number == original);

    private static uint? ChangedResource(decimal? value, uint original) =>
        WholeNumber(value, out uint number) && number != original ? number : null;

    private void CharacterResources_Changed(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_refreshingCharacters || _character is null) return;
        if (!CharacterValuesValid())
        {
            ShowStatus(UiLanguage.Get("InvalidValue"));
            return;
        }
        _character.SetResources(
            ap: ChangedResource(APInput.Value, _character.AP),
            affinityCoins: _character.UsesAffinityCoins ? ChangedResource(AffinityCoinsInput.Value, _character.AffinityCoins) : null,
            reserveExperience: ChangedResource(ReserveExperienceInput.Value, _character.ReserveExperience));
        ShowStatus(null);
    }

    private void MaxAP_Click(object? sender, RoutedEventArgs e)
    {
        if (_character is { } character)
            ApplyCharacterEdit(() => character.SetResources(ap: CharacterRecord.MaximumAP));
    }

    private void MaxReserveExperience_Click(object? sender, RoutedEventArgs e)
    {
        if (_character is { } character)
            ApplyCharacterEdit(() => character.SetResources(reserveExperience: CharacterRecord.MaximumReserveExperience));
    }

    private void MaxAllAP_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is not null) ApplyCharacterEdit(Session.Document.MaxAllAP);
    }

    private void ApplyCharacterEdit(Action edit)
    {
        if (!CommitProgressionDraft()) return;
        edit();
        RefreshCharacters();
        ShowStatus(null);
    }

    private void LevelInput_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingCharacters || _character is null) return;
        if (LevelInput.SelectedItem is uint level && level != _character.Level)
            ExperienceInput.Value = 0;
    }

    private void ApplyProgression_Click(object? sender, RoutedEventArgs e)
    {
        if (!CommitProgressionDraft()) return;
        RefreshCharacters();
        ShowStatus(null);
    }

    private bool CommitProgressionDraft()
    {
        if (_character is null) return true;
        if (!CharacterValuesValid())
        {
            ShowStatus(UiLanguage.Get("InvalidValue"));
            return false;
        }
        if (!_character.CanEditProgression) return true;
        uint? level = LevelInput.SelectedItem is uint selected && selected != _character.Level ? selected : null;
        uint? experience = ChangedResource(ExperienceInput.Value, _character.Experience);
        if (level is null && experience is null) return true;
        _character.SetProgression(level, experience);
        return true;
    }

    private bool HasProgressionDraft => _character is { CanEditProgression: true }
        && (LevelInput.SelectedItem is not uint level || level != _character.Level
            || ExperienceInput.Value != _character.Experience);
}
