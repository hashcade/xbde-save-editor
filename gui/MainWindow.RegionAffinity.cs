using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record RegionPointsDraft(decimal? Value, string Text);
    private sealed record RegionAffinityRow(RegionAffinityRecord Region, RegionPointsDraft? Draft)
    {
        public string Name => Region.Definition.Name;
        public int Points => Region.Points;
        public int Stars => Region.Stars;
        public int[] StarChoices => [1, 2, 3, 4, 5];
        public bool CanEdit => Region.CanEdit;
        public bool ReadOnly => !CanEdit;
        public int DisplayMaximum => Math.Max(RegionAffinityRecord.MaximumPoints, Points);
        public decimal? DraftValue => Draft is null ? Points : Draft.Value;
    }

    private readonly Dictionary<int, RegionPointsDraft> _regionDrafts = [];
    private SaveDocument? _regionDraftDocument;
    private bool _refreshingRegions;
    private int _affinityTabIndex;

    public void ShowRegionAffinity()
    {
        ShowAffinity();
        AffinityNavigation.SelectedIndex = 1;
    }

    private void RefreshRegionAffinity()
    {
        if (RegionAffinityCards is null) return;
        _refreshingRegions = true;
        try
        {
            if (!ReferenceEquals(_regionDraftDocument, Session?.Document))
            {
                _regionDrafts.Clear();
                _regionDraftDocument = Session?.Document;
            }
            var regions = Session?.Document.RegionAffinities ?? [];
            RegionAffinityCards.ItemsSource = regions.Select(region => new RegionAffinityRow(region, _regionDrafts.GetValueOrDefault(region.Id))).ToArray();
            MaxAllRegionsButton.IsEnabled = Session?.Document.CanEditRegionAffinity == true && regions.Any(region => !region.IsMaximum);
            RegionAffinityUnavailable.IsVisible = Session is not null && !Session.Document.CanEditRegionAffinity;
            CharacterAffinityPanel.IsVisible = AffinityNavigation.SelectedIndex == 0;
            RegionAffinityScroll.IsVisible = AffinityNavigation.SelectedIndex == 1;
        }
        finally { _refreshingRegions = false; }
    }

    private void AffinityNavigation_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingRegions || AffinityNavigation is null || RegionAffinityCards is null) return;
        if (!CommitAffinityDraft() || !CommitRegionAffinityDrafts())
        {
            _refreshingRegions = true;
            AffinityNavigation.SelectedIndex = _affinityTabIndex;
            _refreshingRegions = false;
            return;
        }
        _affinityTabIndex = AffinityNavigation.SelectedIndex;
        RefreshRegionAffinity();
    }

    private void RegionPoints_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not NumericUpDown { Tag: RegionAffinityRow row } input) return;
        input.PropertyChanged -= RegionPointsText_Changed;
        bool refreshing = _refreshingRegions;
        _refreshingRegions = true;
        try
        {
            if (_regionDrafts.TryGetValue(row.Region.Id, out var draft)) input.Text = draft.Text;
        }
        finally { _refreshingRegions = refreshing; }
        input.PropertyChanged += RegionPointsText_Changed;
    }

    private void RegionPointsText_Changed(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != NumericUpDown.TextProperty || sender is not NumericUpDown input) return;
        string text = input.Text ?? "";
        decimal? value = decimal.TryParse(text, NumberStyles.Number, input.NumberFormat, out decimal parsed) ? parsed : null;
        StoreRegionDraft(input, value, text);
    }

    private void RegionPoints_Changed(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (sender is NumericUpDown input)
            StoreRegionDraft(input, input.Value, input.Value?.ToString("0.############################", input.NumberFormat) ?? "");
    }

    private void StoreRegionDraft(NumericUpDown input, decimal? value, string text)
    {
        if (_refreshingRegions || !input.IsLoaded || input.Tag is not RegionAffinityRow { CanEdit: true } row) return;
        if (value == row.Points) _regionDrafts.Remove(row.Region.Id);
        else _regionDrafts[row.Region.Id] = new(value, text);
    }

    private bool CommitRegionAffinityDrafts()
    {
        if (_regionDrafts.Count == 0) return true;
        if (Session?.Document.CanEditRegionAffinity != true) return false;
        var edits = new List<(RegionAffinityRecord Region, int Points)>();
        foreach (var (id, draft) in _regionDrafts)
        {
            var region = Session.Document.GetRegionAffinity(id);
            if (!WholeNumber(draft.Value, out uint points)
                || (points > RegionAffinityRecord.MaximumPoints && points != region.Points))
            {
                ShowStatus(UiLanguage.Get("InvalidValue"));
                return false;
            }
            if (points != region.Points) edits.Add((region, (int)points));
        }
        foreach (var (region, points) in edits) region.SetPoints(points);
        _regionDrafts.Clear();
        return true;
    }

    private void ApplyRegionPoints_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RegionAffinityRow { CanEdit: true } row }) return;
        decimal? value = _regionDrafts.TryGetValue(row.Region.Id, out var draft) ? draft.Value : row.DraftValue;
        if (!WholeNumber(value, out uint points) || (points > RegionAffinityRecord.MaximumPoints && points != row.Points))
        {
            ShowStatus(UiLanguage.Get("InvalidValue"));
            return;
        }
        if (points != row.Points) row.Region.SetPoints((int)points);
        _regionDrafts.Remove(row.Region.Id);
        RefreshRegionAffinity();
        ShowStatus(null);
    }

    private void RegionStars_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingRegions || sender is not ComboBox { IsLoaded: true, Tag: RegionAffinityRow { CanEdit: true } row, SelectedItem: int stars }
            || stars == row.Stars) return;
        if (!CommitRegionAffinityDrafts())
        {
            _refreshingRegions = true;
            ((ComboBox)sender).SelectedItem = row.Stars;
            _refreshingRegions = false;
            return;
        }
        row.Region.SetStars(stars);
        RefreshRegionAffinity();
        ShowStatus(null);
    }

    private void MaxAllRegions_Click(object? sender, RoutedEventArgs e)
    {
        if (Session?.Document.CanEditRegionAffinity != true || !CommitRegionAffinityDrafts()) return;
        Session.Document.MaxAllRegionAffinity();
        RefreshRegionAffinity();
        ShowStatus(null);
    }
}
