using Avalonia.Controls;
using Avalonia.Interactivity;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record AchievementRow(int Id, string Label);
    private int? _selectedAchievement;
    private bool _refreshingAchievements;

    public void ShowAchievements() => MainNavigation.SelectedIndex = 3;

    private void RefreshAchievements()
    {
        if (AchievementList is null) return;
        _refreshingAchievements = true;
        try
        {
            int category = Math.Max(0, AchievementCategoryFilter.SelectedIndex);
            int status = Math.Max(0, AchievementStatusFilter.SelectedIndex);
            AchievementCategoryFilter.ItemsSource = new[] { UiLanguage.Get("AllCategories"), UiLanguage.Get("Trials"), UiLanguage.Get("Records") };
            AchievementStatusFilter.ItemsSource = new[] { UiLanguage.Get("AllStatuses"), UiLanguage.Get("AchievementIncomplete"),
                UiLanguage.Get("AchievementComplete"), UiLanguage.Get("AchievementUnmetCounterFilter") };
            AchievementCategoryFilter.SelectedIndex = category;
            AchievementStatusFilter.SelectedIndex = status;
            bool supported = Session?.Document.CanEditAchievements == true;
            var achievements = supported ? Session!.Document.Achievements : [];
            AchievementCountValue.Text = supported ? $"{achievements.Count(item => item.Completed)}/{achievements.Count}" : null;
            AchievementCategoryFilter.IsEnabled = AchievementStatusFilter.IsEnabled = AchievementSearch.IsEnabled = supported;
            AchievementsUnavailableValue.IsVisible = Session is not null && !supported;
            UnlockAllAchievementsButton.IsEnabled = supported && achievements.Any(item => !item.Completed);
            string search = AchievementSearch.Text?.Trim() ?? "";
            var rows = achievements.Where(item => (category == 0 || (int)item.Definition.Category == category)
                && MatchesAchievementStatus(item, status)
                && (item.Definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.Definition.Condition.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(item => item.Definition.Category).ThenBy(item => item.Definition.Order)
                .Select(item => new AchievementRow(item.Id, item.Definition.Name)).ToArray();
            AchievementList.ItemsSource = rows;
            var selected = rows.FirstOrDefault(row => row.Id == _selectedAchievement) ?? rows.FirstOrDefault();
            AchievementList.SelectedItem = selected;
            _selectedAchievement = selected?.Id;
            RefreshAchievementDetails();
        }
        finally { _refreshingAchievements = false; }
    }

    private void RefreshAchievementDetails()
    {
        AchievementDetails.IsVisible = _selectedAchievement is not null;
        if (_selectedAchievement is not int id || Session is null) return;
        var item = Session.Document.GetAchievement(id);
        AchievementConditionValue.Text = item.Definition.Condition;
        AchievementCategoryValue.Text = UiLanguage.Get(item.Definition.Category.ToString());
        string status = item.Completed ? "AchievementComplete" : "AchievementIncomplete";
        if (item.HasUnmetCompletedCounter) status = "AchievementUnmetCounter";
        AchievementStatusValue.Text = UiLanguage.Get(status);
        AchievementRewardValue.Text = item.Definition.RewardExperience.ToString("N0");
        AchievementProgressTitle.IsVisible = AchievementProgressValue.IsVisible = item.Progress is not null;
        AchievementProgressValue.Text = item.Progress is int progress ? $"{progress:N0} / {item.Definition.Required:N0}" : null;
        UnlockAchievementButton.IsEnabled = item.CanUnlock;
    }

    private static bool MatchesAchievementStatus(AchievementRecord item, int status) => status switch
    {
        1 => !item.Completed,
        2 => item.Completed,
        3 => item.HasUnmetCompletedCounter,
        _ => true
    };

    private void AchievementList_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingAchievements) return;
        _selectedAchievement = (AchievementList.SelectedItem as AchievementRow)?.Id;
        RefreshAchievementDetails();
    }

    private void AchievementFilter_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (!_refreshingAchievements) RefreshAchievements();
    }

    private void AchievementSearch_Changed(object? sender, TextChangedEventArgs e)
    {
        if (!_refreshingAchievements) RefreshAchievements();
    }

    private void UnlockAchievement_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedAchievement is not int id || Session is null) return;
        try
        {
            Session.Document.GetAchievement(id).Unlock();
            RefreshAchievements();
            ShowStatus(null);
        }
        catch (Exception error) when (IsExpected(error)) { ShowStatus(error.Message); }
    }

    private void UnlockAllAchievements_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is null) return;
        try
        {
            Session.Document.UnlockAllAchievements();
            RefreshAchievements();
            ShowStatus(null);
        }
        catch (Exception error) when (IsExpected(error)) { ShowStatus(error.Message); }
    }
}
