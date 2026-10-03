using Avalonia.Controls;
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
        if (MainScroll is null || CharactersPanel is null) return;
        MainScroll.IsVisible = MainNavigation.SelectedIndex == 0;
        CharactersPanel.IsVisible = MainNavigation.SelectedIndex == 1;
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
            LevelValue.Text = _character?.Level.ToString();
            ExperienceValue.Text = _character?.Experience.ToString();
            ExpertLevelValue.Text = _character?.ExpertLevel.ToString();
            ExpertExperienceValue.Text = _character?.ExpertExperience.ToString();
            ReserveExperienceValue.Text = _character?.ReserveExperience.ToString();
            APInput.Value = _character?.AP;
            AffinityCoinsField.IsVisible = _character?.UsesAffinityCoins ?? true;
            AffinityCoinsInput.Maximum = Math.Max(999, _character?.AffinityCoins ?? 0);
            AffinityCoinsInput.Value = _character?.AffinityCoins;
        }
        finally { _refreshingCharacters = false; }
    }

    private void CharacterList_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingCharacters) return;
        _selectedCharacter = (CharacterList.SelectedItem as CharacterRow)?.Id;
        RefreshCharacters();
    }

    private void CharacterSearch_Changed(object? sender, TextChangedEventArgs e) => RefreshCharacters();

    private bool CharacterValuesValid()
    {
        if (_character is null) return true;
        if (!WholeNumber(APInput.Value, out _)) return false;
        if (!_character.UsesAffinityCoins) return true;
        return WholeNumber(AffinityCoinsInput.Value, out uint coins)
            && (coins <= 999 || coins == _character.AffinityCoins);
    }

    private void CharacterResources_Changed(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_refreshingCharacters || _character is null) return;
        if (!CharacterValuesValid())
        {
            ShowStatus(UiLanguage.Get("InvalidValue"));
            return;
        }
        _ = WholeNumber(APInput.Value, out uint ap);
        uint? coins = null;
        if (_character.UsesAffinityCoins && WholeNumber(AffinityCoinsInput.Value, out uint enteredCoins)
            && enteredCoins != _character.AffinityCoins) coins = enteredCoins;
        _character.SetResources(ap, coins);
        ShowStatus(null);
    }
}
