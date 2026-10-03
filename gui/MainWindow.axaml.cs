using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow : Window
{
    private bool _refreshing;
    private bool _allowClose;
    public SaveSession? Session { get; private set; }
    private bool HasUnsavedChanges => Session?.HasChanges == true || HasProgressionDraft
        || HasGemDraft || HasInventoryDraft || _skillProgressDrafts.Count != 0 || _regionDrafts.Count != 0;

    public MainWindow()
    {
        InitializeComponent();
        Closing += Window_Closing;
        foreach (var (code, name) in UiLanguage.Languages)
        {
            var item = new MenuItem { Header = name };
            item.Click += (_, _) => SetLanguage(code);
            LanguageMenu.Items.Add(item);
        }
        RefreshAchievements();
        RefreshAffinity();
        RefreshCollectopaedia();
    }

    public bool LoadSave(string path)
    {
        try
        {
            var session = SaveSession.Open(path);
            Session = session;
            _gem = null;
            _inventoryItem = null;
            _inventorySelections.Clear();
            _equipmentInventorySelections.Clear();
            _equipmentRecipient = null;
            _selectedAchievement = null;
            _collectionMap = null;
            _collectionEntry = null;
            _selectedAffinity = null;
            _affinity = null;
            RefreshMain();
            RefreshCharacters();
            RefreshGems();
            RefreshInventory();
            RefreshEquipmentInventory();
            RefreshAchievements();
            RefreshCollectopaedia();
            RefreshAffinity();
            ShowStatus(null);
            return true;
        }
        catch (Exception error) when (IsExpected(error))
        {
            ShowStatus(UiLanguage.Get("OpenFailed") + " " + error.Message);
            return false;
        }
    }

    public bool SaveTo(string path)
    {
        if (Session is null) return false;
        if (!ResourceValuesValid(out _, out _))
        {
            ShowStatus(UiLanguage.Get("InvalidValue"));
            return false;
        }
        if (!CharacterValuesValid())
        {
            ShowStatus(UiLanguage.Get("InvalidValue"));
            return false;
        }
        try
        {
            if (!CommitSkillDrafts()) return false;
            if (!CommitProgressionDraft()) return false;
            if (!CommitGemDraft()) return false;
            if (!CommitInventoryDraft()) return false;
            if (!CommitAffinityDraft()) return false;
            if (!CommitRegionAffinityDrafts()) return false;
            Session.Save(path);
            RefreshCharacters();
            RefreshGems();
            RefreshInventory();
            RefreshEquipmentInventory();
            RefreshRegionAffinity();
            ShowStatus(UiLanguage.Get("Saved") + " " + Path.GetFileName(path));
            return true;
        }
        catch (Exception error) when (IsExpected(error))
        {
            ShowStatus(UiLanguage.Get("SaveFailed") + " " + error.Message);
            return false;
        }
    }

    public void SetLanguage(string language)
    {
        if (!CommitProgressionDraft()) return;
        if (!CommitGemDraft()) return;
        if (!CommitInventoryDraft()) return;
        if (!CommitAffinityDraft()) return;
        if (!CommitRegionAffinityDrafts()) return;
        UiLanguage.Apply(language);
        RefreshMain();
        RefreshCharacters();
        RefreshGems();
        RefreshInventory();
        RefreshEquipmentInventory();
        RefreshAchievements();
        RefreshCollectopaedia();
        RefreshAffinity();
        ShowStatus(null);
    }

    private void RefreshMain()
    {
        _refreshing = true;
        try
        {
            RefreshColony6();
            ResourceInputs.IsEnabled = SaveMenu.IsEnabled = SaveAsMenu.IsEnabled = Session is not null;
            if (Session is null) return;
            MoneyInput.Maximum = Math.Max(SaveDocument.MaximumCurrency, Session.Document.Money);
            NoponstonesInput.Maximum = Math.Max(SaveDocument.MaximumCurrency, Session.Document.Noponstones);
            MoneyInput.Value = Session.Document.Money;
            NoponstonesInput.Value = Session.Document.Noponstones;
            FileNameValue.Text = Path.GetFileName(Session.SourcePath);
            CampaignValue.Text = UiLanguage.Get(Session.Document.Campaign.ToString());
            CharacterCountValue.Text = Session.Document.Characters.Count.ToString();
        }
        finally { _refreshing = false; }
    }

    private void Resources_Changed(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_refreshing || Session is null) return;
        if (!ResourceValuesValid(out uint money, out uint noponstones))
        {
            ShowStatus(UiLanguage.Get("InvalidValue"));
            return;
        }
        Session.Document.SetResources(money == Session.Document.Money ? null : money,
            noponstones == Session.Document.Noponstones ? null : noponstones);
        ShowStatus(null);
    }

    private bool ResourceValuesValid(out uint money, out uint noponstones)
    {
        money = noponstones = 0;
        return Session is { } session
            && WholeNumber(MoneyInput.Value, out money)
            && WholeNumber(NoponstonesInput.Value, out noponstones)
            && (money <= SaveDocument.MaximumCurrency || money == session.Document.Money)
            && (noponstones <= SaveDocument.MaximumCurrency || noponstones == session.Document.Noponstones);
    }

    public static bool WholeNumber(decimal? value, out uint number)
    {
        number = 0;
        if (value is null || value < 0 || value > uint.MaxValue || value != decimal.Truncate(value.Value)) return false;
        number = (uint)value.Value;
        return true;
    }

    private void ShowStatus(string? message)
    {
        StatusMessage.Text = message;
        StatusMessage.IsVisible = message is not null;
    }

    private static bool IsExpected(Exception error) => error is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException;

    private async void OpenSave_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscard()) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UiLanguage.Get("OpenSave"), AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("XBDE saves") { Patterns = ["*.sav"] }]
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) LoadSave(path);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is not null) SaveTo(Session.SourcePath);
    }

    private async void SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is null) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = UiLanguage.Get("SaveAs"), SuggestedFileName = Path.GetFileName(Session.SourcePath),
            DefaultExtension = "sav", ShowOverwritePrompt = true
        });
        if (file?.TryGetLocalPath() is { } path) SaveTo(path);
    }

    private async Task<bool> ConfirmDiscard()
    {
        if (!HasUnsavedChanges) return true;
        var discard = new Button { Content = UiLanguage.Get("Discard") };
        var cancel = new Button { Content = UiLanguage.Get("Cancel") };
        var dialog = new Window
        {
            Title = UiLanguage.Get("UnsavedChanges"), Width = 380, SizeToContent = SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24), Spacing = 16,
                Children =
                {
                    new TextBlock { Text = UiLanguage.Get("DiscardQuestion"), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 16, Children = { cancel, discard } }
                }
            }
        };
        discard.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        return await dialog.ShowDialog<bool>(this);
    }

    private async void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose || !HasUnsavedChanges) return;
        e.Cancel = true;
        if (await ConfirmDiscard())
        {
            _allowClose = true;
            Close();
        }
    }

    private void Exit_Click(object? sender, RoutedEventArgs e) => Close();

    private async void About_Click(object? sender, RoutedEventArgs e) => await new AboutWindow().ShowDialog(this);
}
