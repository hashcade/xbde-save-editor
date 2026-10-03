using Avalonia.Controls;
using Avalonia.Interactivity;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record AffinityRow(int Index, string Label);
    private AffinityRecord? _affinity;
    private int? _selectedAffinity;
    private bool _refreshingAffinity;

    public void ShowAffinity() => MainNavigation.SelectedIndex = 3;

    private void RefreshAffinity()
    {
        if (AffinityList is null) return;
        _refreshingAffinity = true;
        try
        {
            var pairs = Session?.Document.Affinities ?? [];
            bool supported = Session?.Document.CanEditAffinity == true;
            string search = AffinitySearch.Text?.Trim() ?? "";
            var rows = pairs.Select(pair => new AffinityRow(pair.Index,
                $"{CharacterCatalog.Get(pair.FirstCharacterId, UiLanguage.Current)} — {CharacterCatalog.Get(pair.SecondCharacterId, UiLanguage.Current)}"))
                .Where(row => row.Label.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
            AffinityList.ItemsSource = rows;
            AffinityList.SelectedItem = rows.FirstOrDefault(row => row.Index == _selectedAffinity) ?? rows.FirstOrDefault();
            _selectedAffinity = (AffinityList.SelectedItem as AffinityRow)?.Index;
            _affinity = pairs.FirstOrDefault(pair => pair.Index == _selectedAffinity);
            AffinityCountValue.Text = pairs.Count > 0 ? $"{pairs.Count(pair => pair.IsMaximum)}/{pairs.Count}" : null;
            MaxAllAffinityButton.IsEnabled = supported && pairs.Any(pair => !pair.IsMaximum);
            AffinityUnavailableValue.IsVisible = Session is not null && !supported;
            AffinityDetails.IsVisible = _affinity is not null;
            AffinityPointsInput.IsVisible = _affinity?.CanEdit == true;
            AffinityPointsValue.IsVisible = _affinity?.CanEdit == false;
            AffinityPointsInput.Maximum = Math.Max(AffinityCatalog.MaximumPoints, _affinity?.Points ?? 0);
            AffinityPointsInput.Value = _affinity?.Points;
            AffinityPointsValue.Text = _affinity?.Points.ToString("N0");
            AffinitySlotsValue.Text = _affinity is null ? null
                : $"{_affinity.FirstUnlockedSlots?.ToString() ?? "—"} / 5 · {_affinity.SecondUnlockedSlots?.ToString() ?? "—"} / 5";
            MaxAffinityButton.IsEnabled = _affinity is { CanEdit: true, IsMaximum: false };
        }
        finally { _refreshingAffinity = false; }
    }

    private bool CommitAffinityDraft()
    {
        if (_affinity is not { CanEdit: true }) return true;
        if (!WholeNumber(AffinityPointsInput.Value, out uint points)
            || (points > AffinityCatalog.MaximumPoints && points != _affinity.Points))
        {
            ShowStatus(UiLanguage.Get("InvalidValue"));
            return false;
        }
        if (points != _affinity.Points) _affinity.SetPoints((int)points);
        return true;
    }

    private void AffinityPoints_Changed(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_refreshingAffinity || !CommitAffinityDraft()) return;
        RefreshAffinity();
        RefreshSkillLinks();
        ShowStatus(null);
    }

    private void AffinityList_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingAffinity) return;
        if (!CommitAffinityDraft())
        {
            _refreshingAffinity = true;
            AffinityList.SelectedItem = AffinityList.Items.OfType<AffinityRow>().FirstOrDefault(row => row.Index == _selectedAffinity);
            _refreshingAffinity = false;
            return;
        }
        _selectedAffinity = (AffinityList.SelectedItem as AffinityRow)?.Index;
        RefreshAffinity();
    }

    private void AffinitySearch_Changed(object? sender, TextChangedEventArgs e)
    {
        if (!_refreshingAffinity && CommitAffinityDraft()) RefreshAffinity();
    }

    private void MaxAffinity_Click(object? sender, RoutedEventArgs e)
    {
        if (_affinity is not { CanEdit: true } || !CommitAffinityDraft()) return;
        _affinity.SetPoints(AffinityCatalog.MaximumPoints);
        RefreshAffinity();
        RefreshSkillLinks();
        ShowStatus(null);
    }

    private void MaxAllAffinity_Click(object? sender, RoutedEventArgs e)
    {
        if (Session?.Document.CanEditAffinity != true || !CommitAffinityDraft()) return;
        Session.Document.MaxAllAffinity();
        RefreshAffinity();
        RefreshSkillLinks();
        ShowStatus(null);
    }
}
