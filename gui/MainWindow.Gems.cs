using Avalonia.Controls;
using Avalonia.Interactivity;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record GemRow(int Index, string Label);
    private sealed record GemEffectChoice(int Id, string Label);
    private sealed record GemRankChoice(int Rank, string Label);
    private GemRecord? _gem;
    private int? _selectedGem;
    private bool _refreshingGems;

    public void ShowGems() => MainNavigation.SelectedIndex = 2;

    private GemDefinition? GemDraftDefinition => GemEffectInput.SelectedItem is GemEffectChoice effect
        && GemRankInput.SelectedItem is GemRankChoice rank ? GemCatalog.Find(effect.Id, rank.Rank) : null;

    private bool HasGemDraft => _gem?.CanEdit == true && (GemDraftDefinition is not { } rule
        || rule.EffectId != _gem.EffectId || rule.Rank != _gem.Rank || GemStrengthInput.Value != _gem.Strength);

    private void RefreshGems()
    {
        if (GemList is null) return;
        _refreshingGems = true;
        try
        {
            string query = GemSearch.Text?.Trim() ?? "";
            var rows = Session?.Document.Gems.Where(gem => !gem.IsCylinder)
                .Select(gem => new GemRow(gem.Index, gem.Label))
                .Where(row => row.Label.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];
            GemList.ItemsSource = rows;
            GemList.SelectedItem = rows.FirstOrDefault(row => row.Index == _selectedGem) ?? rows.FirstOrDefault();
            _selectedGem = (GemList.SelectedItem as GemRow)?.Index;
            _gem = _selectedGem is { } index ? Session?.Document.GetGem(index) : null;
            GemInputs.IsEnabled = _gem?.CanEdit ?? false;
            IEnumerable<GemDefinition> definitions = _gem?.AvailableDefinitions ?? GemCatalog.Definitions;
            if (_gem?.Definition is { } existing) definitions = definitions.Append(existing);
            var effects = definitions.DistinctBy(rule => rule.EffectId).ToArray();
            var choices = effects.Select(rule => new GemEffectChoice(rule.EffectId,
                effects.Count(other => other.Name == rule.Name) > 1 ? $"{rule.Name} ({rule.EffectId})" : rule.Name)).ToArray();
            GemEffectInput.ItemsSource = choices;
            GemEffectInput.SelectedItem = choices.FirstOrDefault(choice => choice.Id == _gem?.EffectId);
            SetGemRanks(_gem?.Rank ?? 1);
            UpdateGemBounds(_gem?.Strength ?? 0, preserve: true);
        }
        finally { _refreshingGems = false; }
    }

    private void SetGemRanks(int preferred)
    {
        int? effect = (GemEffectInput.SelectedItem as GemEffectChoice)?.Id;
        string[] labels = ["I", "II", "III", "IV", "V", "VI"];
        var ranks = GemCatalog.Definitions.Where(rule => rule.EffectId == effect)
            .Select(rule => new GemRankChoice(rule.Rank, labels[rule.Rank - 1])).ToArray();
        GemRankInput.ItemsSource = ranks;
        GemRankInput.SelectedItem = ranks.FirstOrDefault(rank => rank.Rank == preferred) ?? ranks.FirstOrDefault();
    }

    private void UpdateGemBounds(decimal value, bool preserve = false)
    {
        var rule = GemDraftDefinition;
        GemStrengthField.IsVisible = rule?.Maximum > 0;
        GemChanceField.IsVisible = rule?.Chance > 0;
        GemChanceValue.Text = rule is null ? "" : $"{rule.Chance}%";
        GemRangeValue.Text = rule is null ? "" : $"{rule.Minimum}–{rule.Maximum}{rule.Unit}";
        // Loading never clamps an existing out-of-range gem; an explicit definition change does.
        GemStrengthInput.Minimum = 0;
        GemStrengthInput.Maximum = 255;
        GemStrengthInput.Value = preserve ? value : Math.Clamp(value, rule?.Minimum ?? 0, rule?.Maximum ?? 0);
        GemStrengthInput.Minimum = preserve ? Math.Min(value, rule?.Minimum ?? 0) : rule?.Minimum ?? 0;
        GemStrengthInput.Maximum = preserve ? Math.Max(value, rule?.Maximum ?? 0) : rule?.Maximum ?? 0;
        MaxGemButton.IsVisible = rule?.Maximum > 0;
    }

    private void GemEffect_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingGems) return;
        int rank = (GemRankInput.SelectedItem as GemRankChoice)?.Rank ?? 1;
        decimal strength = GemStrengthInput.Value ?? 0;
        _refreshingGems = true;
        try { SetGemRanks(rank); UpdateGemBounds(strength); }
        finally { _refreshingGems = false; }
    }

    private void GemRank_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (!_refreshingGems) UpdateGemBounds(GemStrengthInput.Value ?? 0);
    }

    private void GemList_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingGems) return;
        if (!CommitGemDraft())
        {
            _refreshingGems = true;
            GemList.SelectedItem = GemList.Items.OfType<GemRow>().FirstOrDefault(row => row.Index == _selectedGem);
            _refreshingGems = false;
            return;
        }
        _selectedGem = (GemList.SelectedItem as GemRow)?.Index;
        RefreshGems();
        RefreshEquipment();
    }

    private void GemSearch_Changed(object? sender, TextChangedEventArgs e)
    {
        if (CommitGemDraft()) { RefreshGems(); RefreshEquipment(); }
    }

    private bool CommitGemDraft()
    {
        if (!HasGemDraft) return true;
        if (GemDraftDefinition is not { } rule || !WholeNumber(GemStrengthInput.Value, out uint value)
            || value < rule.Minimum || value > rule.Maximum)
        {
            ShowStatus(UiLanguage.Get("InvalidValue"));
            return false;
        }
        try
        {
            _gem!.Set(rule.EffectId, rule.Rank, (int)value);
            ShowStatus(null);
            return true;
        }
        catch (ArgumentException) { ShowStatus(UiLanguage.Get("InvalidGemEffect")); return false; }
    }

    private void ApplyGem_Click(object? sender, RoutedEventArgs e)
    {
        if (!CommitGemDraft()) return;
        RefreshGemViews();
    }

    private void MaxGem_Click(object? sender, RoutedEventArgs e)
    {
        if (_gem?.CanEdit != true || GemDraftDefinition is not { } rule) return;
        try
        {
            _gem.Set(rule.EffectId, rule.Rank, rule.Maximum);
            RefreshGemViews();
        }
        catch (ArgumentException) { ShowStatus(UiLanguage.Get("InvalidGemEffect")); }
    }

    private void RefreshGemViews()
    {
        RefreshGems();
        RefreshEquipment();
        ShowStatus(null);
    }
}
