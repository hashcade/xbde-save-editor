using Avalonia.Controls;
using Avalonia.Interactivity;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record CollectionPageChoice(int MapId, string Label)
    {
        public override string ToString() => Label;
    }
    private sealed record CollectionRow(int Id, string Label);
    private sealed record CollectionRewardRow(string Label, string Progress);
    private int? _collectionMap;
    private int? _collectionEntry;
    private bool _refreshingCollection;

    public void ShowCollectopaedia() => MainNavigation.SelectedIndex = 5;

    private void RefreshCollectopaedia()
    {
        if (CollectionList is null) return;
        _refreshingCollection = true;
        try
        {
            var document = Session?.Document;
            var entries = document?.Collectopaedia ?? [];
            bool supported = document?.CanInspectCollectopaedia == true;
            int status = Math.Max(0, CollectionStatusFilter.SelectedIndex);
            CollectionStatusFilter.ItemsSource = new[] { UiLanguage.Get("AllStatuses"), UiLanguage.Get("Unregistered"), UiLanguage.Get("Registered") };
            CollectionStatusFilter.SelectedIndex = status;
            var pages = entries.GroupBy(entry => entry.MapId).Select(page => new CollectionPageChoice(page.Key,
                $"{page.First().MapName} · {page.Count(entry => entry.IsRegistered)}/{page.Count()}")).ToArray();
            CollectionPageFilter.ItemsSource = pages;
            CollectionPageFilter.SelectedItem = pages.FirstOrDefault(page => page.MapId == _collectionMap) ?? pages.FirstOrDefault();
            _collectionMap = (CollectionPageFilter.SelectedItem as CollectionPageChoice)?.MapId;
            CollectionCountValue.Text = supported ? $"{entries.Count(entry => entry.IsRegistered)}/{entries.Count}" : null;
            CollectionPageFilter.IsEnabled = CollectionStatusFilter.IsEnabled = CollectionSearch.IsEnabled = supported;
            CollectionUnavailableValue.IsVisible = Session is not null && !supported;
            var pageEntries = entries.Where(entry => entry.MapId == _collectionMap).ToArray();
            string search = CollectionSearch.Text?.Trim() ?? "";
            var rows = pageEntries.Where(entry => (status == 0 || entry.IsRegistered == (status == 2))
                && entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.Category).ThenBy(entry => entry.Id)
                .Select(entry => new CollectionRow(entry.Id, entry.Name)).ToArray();
            CollectionList.ItemsSource = rows;
            CollectionList.SelectedItem = rows.FirstOrDefault(row => row.Id == _collectionEntry) ?? rows.FirstOrDefault();
            _collectionEntry = (CollectionList.SelectedItem as CollectionRow)?.Id;
            CompleteCollectionPageButton.IsEnabled = pageEntries.Any(entry => !entry.IsRegistered);
            CompleteCollectionButton.IsEnabled = entries.Any(entry => !entry.IsRegistered);
            CollectionRewardsCard.IsVisible = _collectionMap is not null;
            var pageDefinition = CollectopaediaCatalog.Pages.FirstOrDefault(page => page.Campaign == document?.Campaign && page.MapId == _collectionMap);
            var registered = pageEntries.Where(entry => entry.IsRegistered).Select(entry => entry.Id).ToHashSet();
            CollectionRewardList.ItemsSource = pageDefinition is null ? [] : pageDefinition.Categories
                .Select(category => RewardRow(category.Reward, category.Entries, registered))
                .Append(RewardRow(pageDefinition.Reward, pageDefinition.Entries, registered)).ToArray();
            RefreshCollectionEntry();
        }
        finally { _refreshingCollection = false; }
    }

    private static CollectionRewardRow RewardRow(CollectopaediaRewardDefinition reward,
        IReadOnlyList<CollectopaediaDefinition> entries, IReadOnlySet<int> registered)
    {
        string label = reward.Name;
        if (reward.Gem is { } gem)
        {
            string value = reward.FixedStrength == 0 && gem.Minimum != gem.Maximum
                ? $"{gem.Minimum}–{gem.Maximum}{gem.Unit}" : gem.Describe(reward.FixedStrength, gem.Chance);
            label += $" {new[] { "I", "II", "III", "IV", "V", "VI" }[reward.Rank - 1]} · {value}";
        }
        return new(label, $"{entries.Count(entry => registered.Contains(entry.Id))}/{entries.Count}");
    }

    private void RefreshCollectionEntry()
    {
        var entry = _collectionEntry is int id ? Session?.Document.GetCollectopaediaEntry(id) : null;
        CollectionEntryDetails.IsVisible = entry is not null;
        CollectionEntryStatusValue.Text = entry is null ? null : UiLanguage.Get(entry.IsRegistered ? "Registered" : "Unregistered");
        RegisterCollectionEntryButton.IsEnabled = entry is { IsRegistered: false };
    }

    private void CollectionPage_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingCollection) return;
        _collectionMap = (CollectionPageFilter.SelectedItem as CollectionPageChoice)?.MapId;
        RefreshCollectopaedia();
    }

    private void CollectionFilter_Changed(object? sender, SelectionChangedEventArgs e)
    { if (!_refreshingCollection) RefreshCollectopaedia(); }

    private void CollectionSearch_Changed(object? sender, TextChangedEventArgs e)
    { if (!_refreshingCollection) RefreshCollectopaedia(); }

    private void CollectionList_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingCollection) return;
        _collectionEntry = (CollectionList.SelectedItem as CollectionRow)?.Id;
        RefreshCollectionEntry();
    }

    private void RegisterCollectionEntry_Click(object? sender, RoutedEventArgs e)
    { if (_collectionEntry is int id) ApplyCollectionCompletion([id]); }

    private void CompleteCollectionPage_Click(object? sender, RoutedEventArgs e) =>
        ApplyCollectionCompletion(Session?.Document.Collectopaedia.Where(entry => entry.MapId == _collectionMap).Select(entry => entry.Id) ?? []);

    private void CompleteCollection_Click(object? sender, RoutedEventArgs e) =>
        ApplyCollectionCompletion(Session?.Document.Collectopaedia.Select(entry => entry.Id) ?? []);

    private void ApplyCollectionCompletion(IEnumerable<int> ids)
    {
        if (Session is null) return;
        try
        {
            Session.Document.CompleteCollectopaedia(ids);
            RefreshCollectopaedia();
            RefreshAchievements();
            RefreshEquipmentInventory();
            if (!HasGemDraft) RefreshGems();
            ShowStatus(null);
        }
        catch (Exception error) when (IsExpected(error)) { ShowStatus(error.Message); }
    }
}
