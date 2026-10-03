using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record SkillNodeRow(string Label);
    private sealed record SkillProgressDraft(decimal? Value, string Text);
    private sealed record SkillTreeRow(int CharacterId, SkillTreeRecord Tree, SkillProgressDraft? Draft)
    {
        public string Name => Tree.Name;
        public bool CanEdit => CanEditSkillTree(Tree);
        public bool IsBlocked => !CanEdit;
        public string Status => UiLanguage.Get(Tree.IsUnlocked ? "Unavailable" : "Locked");
        public bool CanEditProgress => CanEdit && Tree.LearnedCount < 5;
        public bool ShowProgressValue => !CanEditProgress;
        public int LearnedCount => Tree.LearnedCount;
        public int[] LearnedCounts
        {
            get
            {
                var counts = Enumerable.Range(Tree.MinimumLearnedCount, 6 - Tree.MinimumLearnedCount);
                if (Tree.LearnedCount < Tree.MinimumLearnedCount || Tree.LearnedCount > 5)
                    counts = counts.Append(Tree.LearnedCount);
                return counts.ToArray();
            }
        }
        public uint Progress => Tree.Progress;
        public decimal? DraftProgress => Draft is null ? Tree.Progress : Draft.Value;
        public uint DisplayMaximum => Math.Max(Tree.MaximumProgress, Tree.Progress);
        public string ProgressRange => CanEditProgress
            ? $"{UiLanguage.Get("SkillProgress")} (0–{Tree.MaximumProgress})"
            : UiLanguage.Get("SkillProgress");
        public SkillNodeRow[] Nodes => Tree.Skills.Select((skill, index) => new SkillNodeRow(
            $"{(index < Tree.LearnedCount ? "✓ " : "")}{skill.Name} · {string.Format(CultureInfo.CurrentCulture, UiLanguage.Get("SkillCost"), skill.RequiredSP)}")).ToArray();
    }

    private readonly Dictionary<(int CharacterId, int TreeIndex), SkillProgressDraft> _skillProgressDrafts = [];
    private SaveDocument? _skillDraftDocument;
    private bool _refreshingSkills;

    public void ShowSkills()
    {
        ShowCharacters();
        CharacterNavigation.SelectedIndex = 2;
    }

    private void RefreshSkills()
    {
        if (SkillTreeList is null) return;
        _refreshingSkills = true;
        try
        {
            if (!ReferenceEquals(_skillDraftDocument, Session?.Document))
            {
                _skillProgressDrafts.Clear();
                _skillDraftDocument = Session?.Document;
            }
            var trees = _character?.SkillTrees ?? [];
            int characterId = _character?.Id ?? 0;
            SkillTreeList.ItemsSource = trees.Select(tree => new SkillTreeRow(characterId, tree,
                _skillProgressDrafts.GetValueOrDefault((characterId, tree.Index)))).ToArray();
            MaxCharacterSkillsButton.IsEnabled = trees.Any(CanEditSkillTree);
            MaxAllSkillsButton.IsEnabled = Session?.Document.Characters
                .Any(character => character.SkillTrees.Any(CanEditSkillTree)) ?? false;
        }
        finally { _refreshingSkills = false; }
    }

    private static bool CanEditSkillTree(SkillTreeRecord tree) => tree.CanEdit
        && tree.LearnedCount >= tree.MinimumLearnedCount && tree.LearnedCount <= 5;

    private void SkillProgress_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not NumericUpDown { Tag: SkillTreeRow row } input) return;
        input.PropertyChanged -= SkillProgressText_Changed;
        bool refreshing = _refreshingSkills;
        _refreshingSkills = true;
        try
        {
            if (_skillProgressDrafts.TryGetValue((row.CharacterId, row.Tree.Index), out var draft))
                input.Text = draft.Text;
        }
        finally { _refreshingSkills = refreshing; }
        input.PropertyChanged += SkillProgressText_Changed;
    }

    private void SkillProgressText_Changed(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != NumericUpDown.TextProperty || sender is not NumericUpDown input) return;
        string text = input.Text ?? "";
        decimal? value = decimal.TryParse(text, NumberStyles.Number, input.NumberFormat, out decimal number)
            ? number : null;
        StoreSkillProgressDraft(input, value, text);
    }

    private void SkillProgress_Changed(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (sender is NumericUpDown input)
            StoreSkillProgressDraft(input, input.Value,
                input.Value?.ToString("0.############################", input.NumberFormat) ?? "");
    }

    private void StoreSkillProgressDraft(NumericUpDown input, decimal? value, string text)
    {
        if (_refreshingSkills || !input.IsLoaded || input.Tag is not SkillTreeRow row || !row.CanEditProgress) return;
        var key = (row.CharacterId, row.Tree.Index);
        if (value == row.Tree.Progress) _skillProgressDrafts.Remove(key);
        else _skillProgressDrafts[key] = new SkillProgressDraft(value, text);
    }

    private void ApplySkillProgress_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SkillTreeRow row } || !row.CanEditProgress) return;
        var key = (row.CharacterId, row.Tree.Index);
        decimal? value = _skillProgressDrafts.TryGetValue(key, out var draft) ? draft.Value : row.DraftProgress;
        if (!WholeNumber(value, out uint progress)
            || (progress > row.Tree.MaximumProgress && progress != row.Tree.Progress))
        {
            ShowStatus(UiLanguage.Get("InvalidValue"));
            return;
        }
        EditSkills(() =>
        {
            // Inspect existing high SP without normalizing it or bypassing SetProgress validation.
            if (progress != row.Tree.Progress) row.Tree.SetProgress(progress);
            _skillProgressDrafts.Remove(key);
        });
    }

    private bool CommitSkillDrafts()
    {
        if (_skillProgressDrafts.Count == 0) return true;
        if (Session is null) return false;
        var edits = new List<(SkillTreeRecord Tree, uint Progress)>();
        foreach (var (key, draft) in _skillProgressDrafts)
        {
            var tree = Session.Document.GetCharacter(key.CharacterId).SkillTrees
                .FirstOrDefault(tree => tree.Index == key.TreeIndex);
            if (tree is null || !CanEditSkillTree(tree) || tree.LearnedCount == 5
                || !WholeNumber(draft.Value, out uint progress)
                || (progress > tree.MaximumProgress && progress != tree.Progress))
            {
                ShowStatus(UiLanguage.Get("InvalidValue"));
                return false;
            }
            if (progress != tree.Progress) edits.Add((tree, progress));
        }
        // Every draft is validated before the first in-memory write.
        foreach (var (tree, progress) in edits) tree.SetProgress(progress);
        _skillProgressDrafts.Clear();
        return true;
    }

    private void SkillLearnedCount_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingSkills || sender is not ComboBox { Tag: SkillTreeRow row, SelectedItem: int count }
            || !row.CanEdit || count == row.Tree.LearnedCount) return;
        EditSkills(() =>
        {
            row.Tree.SetLearnedCount(count);
            _skillProgressDrafts.Remove((row.CharacterId, row.Tree.Index));
        });
    }

    private void MaxTree_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SkillTreeRow row } || !row.CanEdit) return;
        EditSkills(() =>
        {
            row.Tree.Maximize();
            _skillProgressDrafts.Remove((row.CharacterId, row.Tree.Index));
        });
    }

    private void MaxCharacterSkills_Click(object? sender, RoutedEventArgs e)
    {
        if (_character is not { } character) return;
        EditSkills(() =>
        {
            character.MaxSkills();
            foreach (var tree in character.SkillTrees.Where(CanEditSkillTree))
                _skillProgressDrafts.Remove((character.Id, tree.Index));
        });
    }

    private void MaxAllSkills_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is not { } session) return;
        EditSkills(() =>
        {
            session.Document.MaxAllSkills();
            foreach (var character in session.Document.Characters)
                foreach (var tree in character.SkillTrees.Where(CanEditSkillTree))
                    _skillProgressDrafts.Remove((character.Id, tree.Index));
        });
    }

    private void EditSkills(Action edit)
    {
        if (!CommitProgressionDraft())
        {
            RefreshSkills();
            return;
        }
        try
        {
            edit();
            ShowStatus(null);
        }
        catch (ArgumentException) { ShowStatus(UiLanguage.Get("InvalidValue")); }
        RefreshCharacters();
    }
}
