using System.Buffers.Binary;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XbdeEditor.Core;
using XbdeEditor.Gui;
using XbdeEditor.Gui.Localization;

AppBuilder.Configure<App>().UseSkia().WithInterFont()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var window = new MainWindow();
window.Show();
Dispatcher.UIThread.RunJobs();
void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
Check(UiLanguage.Current == "en", "Default language is not English.");
Check(UiLanguage.Read("en")["MaxTree"] == "Max Branch"
    && UiLanguage.Read("zh-Hans")["MaxTree"] == "分支学满"
    && UiLanguage.Read("zh-Hant")["MaxTree"] == "分支學滿",
    "Single-branch maximum label is ambiguous.");
Check(UiLanguage.Read("zh-Hans")["ExpertMode"] == "进阶玩家设定", "Simplified Chinese Expert Mode terminology differs.");
Check(UiLanguage.Read("zh-Hant")["ExpertMode"] == "進階玩家設定", "Traditional Chinese Expert Mode terminology differs.");
Check(!window.FindControl<StackPanel>("ResourceInputs")!.IsEnabled, "Empty form is editable.");
Check(window.FindControl<ScrollViewer>("MainScroll") is not null, "Main page has no shared scroll container.");
VerifyPageSpacing();
void VerifyPageSpacing()
{
    Dispatcher.UIThread.RunJobs();
    var header = window.FindControl<Grid>("Header")!;
    var main = window.FindControl<ScrollViewer>("MainScroll")!;
    var characters = window.FindControl<Grid>("CharactersPanel")!;
    var items = window.FindControl<Grid>("ItemsPanel")!;
    Control page = main;
    if (characters.IsVisible) page = characters;
    else if (items.IsVisible) page = items;
    else if (window.FindControl<Grid>("AffinityPanel")!.IsVisible) page = window.FindControl<Grid>("AffinityPanel")!;
    else if (window.FindControl<Grid>("AchievementsPanel")!.IsVisible) page = window.FindControl<Grid>("AchievementsPanel")!;
    else if (window.FindControl<Grid>("CollectopaediaPanel")!.IsVisible) page = window.FindControl<Grid>("CollectopaediaPanel")!;
    double gap = page.Bounds.Top - header.Bounds.Bottom;
    Check(Math.Abs(gap - (main.IsVisible ? 20 : 16)) < 0.1,
        $"Header-to-page spacing changed: {gap}.");
    if (items.IsVisible && window.FindControl<Grid>("GemsPanel")!.IsVisible)
    {
        var card = window.FindControl<Control>("GemEditorCard")!;
        var scroll = window.FindControl<ScrollViewer>("GemCardsScroll")!;
        var apply = window.FindControl<Button>("ApplyGemButton")!;
        Check(Math.Abs(scroll.Bounds.Right - items.Bounds.Width) < 0.1,
            $"Gem cards do not fill their column: {scroll.Bounds} / {items.Bounds}.");
        if (card.IsVisible)
        {
            var position = apply.TranslatePoint(new Point(0, 0), card)!.Value;
            Check(position.X >= 20 && position.X + apply.Bounds.Width <= card.Bounds.Width - 19.9,
                "Gem editor controls are clipped horizontally.");
        }
    }
    if (!characters.IsVisible) return;
    var tabs = window.FindControl<TabStrip>("CharacterNavigation")!;
    Control body = tabs.SelectedIndex switch
    {
        1 => window.FindControl<Grid>("ArtsPanel")!,
        2 => window.FindControl<Grid>("SkillsPanel")!,
        3 => window.FindControl<ScrollViewer>("EquipmentPanel")!,
        4 => window.FindControl<Grid>("SkillLinksPanel")!,
        _ => window.FindControl<ScrollViewer>("GeneralCharacterScroll")!
    };
    Check(tabs.Margin == new Thickness(0) && Math.Abs(body.Bounds.Top - tabs.Bounds.Bottom - 16) < 0.1,
        "Character tabs have duplicated vertical spacing.");
}
T SkillControl<T>(string name, int treeIndex = 1) where T : Control
{
    Dispatcher.UIThread.RunJobs();
    return window.FindControl<ItemsControl>("SkillTreeList")!.GetVisualDescendants().OfType<T>()
        .Where(control => control.Name == name).ElementAt(treeIndex - 1);
}
Check(!MainWindow.WholeNumber(1.5m, out _), "Fractional amount is accepted.");
Check(!MainWindow.WholeNumber(-1, out _), "Negative amount is accepted.");
Check(!MainWindow.WholeNumber((decimal)uint.MaxValue + 1, out _), "Overflow is accepted.");
string temporary = Path.Combine(Path.GetTempPath(), $"xbde-gui-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporary);
try
{
    EquipmentInventoryGuiTests.Run(window, temporary, Check);
    Colony6GuiTests.Run(window, temporary, Check);
    CollectopaediaGuiTests.Run(window, temporary, Check);
    byte[] regionFixture = new byte[SaveDocument.FileSize];
    BinaryPrimitives.WriteUInt32LittleEndian(regionFixture, 7);
    BinaryPrimitives.WriteUInt16LittleEndian(regionFixture.AsSpan(0x152318), 1);
    BinaryPrimitives.WriteUInt16LittleEndian(regionFixture.AsSpan(0x15231a), 2);
    regionFixture[0x152330] = 2;
    BinaryPrimitives.WriteUInt32LittleEndian(regionFixture.AsSpan(0x152368), 20);
    BinaryPrimitives.WriteUInt32LittleEndian(regionFixture.AsSpan(0x1524a0), 20);
    string regionPath = Path.Combine(temporary, "regions.sav");
    string regionOutput = Path.Combine(temporary, "regions-edited.sav");
    File.WriteAllBytes(regionPath, regionFixture);
    Check(window.LoadSave(regionPath), "Cannot open region test save.");
    window.ShowRegionAffinity();
    Dispatcher.UIThread.RunJobs();
    T RegionControl<T>(string name, int index = 0) where T : Control
    {
        Dispatcher.UIThread.RunJobs();
        return window.FindControl<ItemsControl>("RegionAffinityCards")!.GetVisualDescendants().OfType<T>()
            .Where(control => control.Name == name).ElementAt(index);
    }
    Check(!window.FindControl<Grid>("CharacterAffinityPanel")!.IsVisible
        && window.FindControl<ScrollViewer>("RegionAffinityScroll")!.IsVisible
        && window.FindControl<ItemsControl>("RegionAffinityCards")!.Items.Count == 5,
        "Region affinity did not switch the entire panel or expose five areas.");
    Check(!window.Session!.HasChanges, "Displaying region affinity changed bytes.");
    foreach (bool invalid in new[] { false, true })
    {
        var pendingRegion = RegionControl<NumericUpDown>("RegionPointsInput");
        if (invalid) pendingRegion.Text = "not-a-number";
        else pendingRegion.Value = 100;
        Check(!window.Session.HasChanges, "An unapplied region draft changed the document.");
        window.Close();
        Dispatcher.UIThread.RunJobs();
        var discardDialog = window.OwnedWindows.SingleOrDefault(dialog => dialog.Title == UiLanguage.Get("UnsavedChanges"));
        Check(window.IsVisible && discardDialog is not null,
            "Closing silently discarded an unapplied region draft.");
        discardDialog!.GetVisualDescendants().OfType<Button>()
            .Single(button => Equals(button.Content, UiLanguage.Get("Cancel")))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Check(window.IsVisible && !window.OwnedWindows.Any()
            && pendingRegion.Text == (invalid ? "not-a-number" : "100")
            && File.ReadAllBytes(regionPath).AsSpan().SequenceEqual(regionFixture),
            "Canceling close lost the region draft or changed the source.");
        pendingRegion.Text = "0";
        pendingRegion.Value = 0;
    }
    RegionControl<ComboBox>("RegionStarsInput", 2).SelectedItem = 5;
    byte[] regionExpected = (byte[])regionFixture.Clone();
    BinaryPrimitives.WriteUInt16LittleEndian(regionExpected.AsSpan(0xdf8), 8_000);
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(regionExpected), "GUI stars changed more than their region's points.");
    RegionControl<NumericUpDown>("RegionPointsInput", 2).Value = 9_999;
    RegionControl<Button>("ApplyRegionPointsButton", 2).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    BinaryPrimitives.WriteUInt16LittleEndian(regionExpected.AsSpan(0xdf8), 9_999);
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(regionExpected)
        && (int)RegionControl<ComboBox>("RegionStarsInput", 2).SelectedItem! == 5,
        "GUI point editing did not refresh stars.");
    RegionControl<NumericUpDown>("RegionPointsInput", 0).Value = 100;
    RegionControl<NumericUpDown>("RegionPointsInput", 1).Value = 1.5m;
    Check(!window.SaveTo(regionOutput) && !File.Exists(regionOutput)
        && window.Session.Document.Serialize().AsSpan().SequenceEqual(regionExpected),
        "Invalid region drafts allowed saving or partially committed other regions.");
    window.FindControl<Button>("MaxAllRegionsButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(regionExpected), "Bulk maximum ignored an invalid region draft.");
    window.SetLanguage("ja");
    Check(UiLanguage.Current == "en", "Language switching discarded an invalid region draft.");
    RegionControl<NumericUpDown>("RegionPointsInput", 0).Value = 0;
    RegionControl<NumericUpDown>("RegionPointsInput", 1).Value = 0;
    var invalidRegion = RegionControl<NumericUpDown>("RegionPointsInput");
    invalidRegion.Text = "not-a-number";
    RegionControl<Button>("ApplyRegionPointsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(!window.SaveTo(regionOutput) && window.Session.Document.Serialize().AsSpan().SequenceEqual(regionExpected),
        "Invalid region text fell back to a valid old value.");
    invalidRegion.Text = "0";
    RegionControl<NumericUpDown>("RegionPointsInput").Value = 4_000;
    BinaryPrimitives.WriteUInt16LittleEndian(regionExpected.AsSpan(0xdf4), 4_000);
    Check(window.SaveTo(regionOutput)
        && (int)RegionControl<ComboBox>("RegionStarsInput").SelectedItem! == 3
        && window.Session.Document.Serialize().AsSpan().SequenceEqual(regionExpected),
        "Saving a region draft did not commit points and refresh stars.");
    window.FindControl<Button>("MaxAllRegionsButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (int id = 1; id <= 5; id++) BinaryPrimitives.WriteUInt16LittleEndian(regionExpected.AsSpan(0xdf2 + id * 2), 10_000);
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(regionExpected), "GUI bulk maximum changed unrelated fields.");
    Check(window.SaveTo(regionOutput) && File.ReadAllBytes(regionOutput).AsSpan().SequenceEqual(regionExpected)
        && File.ReadAllBytes(regionPath).AsSpan().SequenceEqual(regionFixture), "GUI region saving changed the source or output bytes.");
    foreach (string language in UiLanguage.Languages.Keys)
    {
        window.SetLanguage(language);
        window.Width = 860;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        VerifyPageSpacing();
        Check(window.FindControl<ItemsControl>("RegionAffinityCards")!.Items.Count == 5
            && !window.FindControl<Button>("MaxAllRegionsButton")!.IsEnabled
            && window.Session.Document.Serialize().AsSpan().SequenceEqual(regionExpected), "Localized regions altered maximum points.");
        var starsInput = RegionControl<ComboBox>("RegionStarsInput");
        var pointsInput = RegionControl<NumericUpDown>("RegionPointsInput");
        Check(starsInput.Bounds.Width > 80 && pointsInput.Bounds.Width > 80, "Region inputs collapsed at minimum window size.");
        Check(RegionControl<TextBlock>("RegionPointsTitle").Text == UiLanguage.Get("RegionPoints"),
            "Region label reused the character-pair point cap.");
        var regionTabs = window.FindControl<TabStrip>("AffinityNavigation")!;
        var regionScroll = window.FindControl<ScrollViewer>("RegionAffinityScroll")!;
        Check(Math.Abs(regionScroll.Bounds.Top - regionTabs.Bounds.Bottom - 16) < 0.1,
            "Region tabs have duplicated vertical spacing.");
    }
    BinaryPrimitives.WriteUInt32LittleEndian(regionFixture, 8);
    File.WriteAllBytes(regionPath, regionFixture);
    Check(window.LoadSave(regionPath), "Cannot inspect an unverified region format.");
    Dispatcher.UIThread.RunJobs();
    Check(!RegionControl<ComboBox>("RegionStarsInput").IsVisible
        && !RegionControl<NumericUpDown>("RegionPointsInput").IsVisible
        && !window.FindControl<Button>("MaxAllRegionsButton")!.IsEnabled, "Unsupported region fields look editable.");
    RegionControl<ComboBox>("RegionStarsInput").SelectedItem = 5;
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(regionFixture), "A protected region callback wrote data.");
    BinaryPrimitives.WriteUInt32LittleEndian(regionFixture, 7);
    BinaryPrimitives.WriteUInt16LittleEndian(regionFixture.AsSpan(0x15231a), 14);
    BinaryPrimitives.WriteUInt32LittleEndian(regionFixture.AsSpan(0x152368 + 13 * 0x138), 20);
    File.WriteAllBytes(regionPath, regionFixture);
    Check(window.LoadSave(regionPath) && window.FindControl<ItemsControl>("RegionAffinityCards")!.Items.Count == 0,
        "Future Connected exposes five main-story regions.");
    window.FindControl<TabStrip>("AffinityNavigation")!.SelectedIndex = 0;
    window.Width = 1120;
    window.Height = 780;
    window.SetLanguage("en");
    byte[] original = new byte[SaveDocument.FileSize];
    BinaryPrimitives.WriteUInt16LittleEndian(original.AsSpan(0x152318), 1);
    BinaryPrimitives.WriteUInt16LittleEndian(original.AsSpan(0x15231a), 2);
    original[0x152330] = 2;
    BinaryPrimitives.WriteUInt32LittleEndian(original.AsSpan(0x152368), 20);
    BinaryPrimitives.WriteUInt32LittleEndian(original.AsSpan(0x1524a0), 20);
    BinaryPrimitives.WriteUInt32LittleEndian(original.AsSpan(0x151b40), 999999999);
    original[0x1536e8] = 1;
    original[0x1536e8 + 22] = 3;
    original[0x1536e8 + 20] = 1;
    string source = Path.Combine(temporary, "bfsgame00.sav");
    byte[] highResources = (byte[])original.Clone();
    BinaryPrimitives.WriteUInt32LittleEndian(highResources.AsSpan(0x151b40), uint.MaxValue);
    BinaryPrimitives.WriteUInt32LittleEndian(highResources.AsSpan(0x10), uint.MaxValue);
    string highResourcesPath = Path.Combine(temporary, "high-resources.sav");
    File.WriteAllBytes(highResourcesPath, highResources);
    Check(window.LoadSave(highResourcesPath), "Could not load existing over-cap currencies.");
    var highMoney = window.FindControl<NumericUpDown>("MoneyInput")!;
    var highStones = window.FindControl<NumericUpDown>("NoponstonesInput")!;
    Check(highMoney.Value == uint.MaxValue && highStones.Value == uint.MaxValue && !window.Session!.HasChanges,
        "GUI clamped existing high currencies.");
    string highCopyPath = Path.Combine(temporary, "high-resource-copy.sav");
    Check(window.SaveTo(highCopyPath) && File.ReadAllBytes(highCopyPath).AsSpan().SequenceEqual(highResources),
        "Saving untouched over-cap currencies changed the file.");
    highMoney.Value = SaveDocument.MaximumCurrency + 1;
    Check(!window.Session!.HasChanges && !window.SaveTo(Path.Combine(temporary, "invalid-currency.sav")),
        "GUI accepted changed over-cap currency.");
    highMoney.Value = 123;
    Check(window.Session.Document.Money == 123 && window.Session.Document.Noponstones == uint.MaxValue,
        "Editing money normalized untouched over-cap Noponstones.");
    highStones.Value = 456;
    Check(window.Session.Document.Money == 123 && window.Session.Document.Noponstones == 456,
        "GUI failed to correct over-cap currencies.");
    File.WriteAllBytes(source, original);
    Check(window.LoadSave(source), "Could not load a game save.");
    Check(!window.Session!.HasChanges, "GUI load changes a save.");
    var money = window.FindControl<NumericUpDown>("MoneyInput")!;
    Check(money.Maximum == SaveDocument.MaximumCurrency
        && window.FindControl<NumericUpDown>("NoponstonesInput")!.Maximum == SaveDocument.MaximumCurrency,
        "GUI currency limits differ from the native cap.");
    Check(money.Value == 999999999, "Existing resource amount was clamped.");
    money.Value = 123;
    Check(window.Session.Document.Money == 123, "GUI edits are not linked to the core.");
    window.ShowCharacters();
    Dispatcher.UIThread.RunJobs();
    var ap = window.FindControl<NumericUpDown>("APInput")!;
    var coins = window.FindControl<NumericUpDown>("AffinityCoinsInput")!;
    var reserve = window.FindControl<NumericUpDown>("ReserveExperienceInput")!;
    Check(ap.Maximum == CharacterRecord.MaximumAP && reserve.Maximum == CharacterRecord.MaximumReserveExperience,
        "GUI resource limits differ from the core.");
    var levelInput = window.FindControl<ComboBox>("LevelInput")!;
    var experienceInput = window.FindControl<NumericUpDown>("ExperienceInput")!;
    var applyProgression = window.FindControl<Button>("ApplyProgressionButton")!;
    Check(levelInput.SelectedItem is 20u, "Character level mapping differs.");
    levelInput.SelectedItem = 1u;
    Check(experienceInput.Value == 0, "Level selection did not reset draft EXP.");
    experienceInput.Value = 101;
    Check(window.Session.Document.GetCharacter(1).Level == 20, "Typing EXP committed a partial number.");
    applyProgression.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetCharacter(1).Level == 3 && experienceInput.Value == 1 && levelInput.SelectedItem is 3u,
        "GUI level/EXP linkage differs from the core.");
    levelInput.SelectedItem = 20u;
    applyProgression.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    experienceInput.Value = 1.5m;
    applyProgression.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetCharacter(1).Experience == 0, "Fractional EXP was committed.");
    Check(!window.SaveTo(Path.Combine(temporary, "invalid-exp.sav")), "Invalid EXP was saved.");
    experienceInput.Value = 0;
    experienceInput.Value = 4;
    window.SetLanguage("ja");
    Check(window.Session.Document.GetCharacter(1).Experience == 4, "Changing language discarded an EXP draft.");
    experienceInput.Value = 0;
    applyProgression.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.FindControl<ListBox>("CharacterList")!.ItemsPanel.Build() is VirtualizingStackPanel { CacheLength: 1 }, "Virtual list buffer is missing.");
    ap.Value = 321;
    coins.Value = 999;
    Check(window.Session.Document.GetCharacter(1).AP == 321 && window.Session.Document.GetCharacter(1).AffinityCoins == 999, "Character resource edits are disconnected.");
    reserve.Value = 10000;
    Check(window.Session.Document.GetCharacter(1).ReserveExperience == 10000, "Reserve EXP input is disconnected.");
    Check(window.Session.Document.GetCharacter(1).Level == 20 && window.Session.Document.GetCharacter(1).Experience == 0,
        "Reserve EXP input altered level or accumulated EXP.");
    foreach (string language in UiLanguage.Languages.Keys)
    {
        window.SetLanguage("zh-Hans");
        window.SetLanguage(language);
        Dispatcher.UIThread.RunJobs();
        VerifyPageSpacing();
        Check(window.FindControl<MenuItem>("SaveMenu")!.Header?.ToString() == UiLanguage.Get("Save"), "Menu translation is stale.");
        Check(window.FindControl<TextBlock>("CampaignValue")!.Text == UiLanguage.Get("MainStory"), "Campaign translation is stale.");
        Check(window.Session.Document.Money == 123, "Language switch changed edits.");
        Check(window.Session.Document.GetCharacter(1).AP == 321, "Language switch changed character edits.");
        Check(window.Session.Document.GetCharacter(1).ReserveExperience == 10000, "Language switch changed reserve EXP.");
        Check(window.FindControl<Button>("MaxAllAPButton")!.Content?.ToString() == UiLanguage.Get("MaxAllAP"),
            "Bulk AP button translation is stale.");
        Check(window.FindControl<Button>("MaxReserveExperienceButton")!.Content?.ToString() == UiLanguage.Get("MaxReserveExperience"),
            "Reserve EXP button translation is stale.");
        Check(applyProgression.Content?.ToString() == UiLanguage.Get("ApplyChanges"), "Progression translation is stale.");
        Check(window.FindControl<TextBlock>("ExpertModeTitle")!.Text == UiLanguage.Get("ExpertMode"),
            "Expert Mode title translation is stale.");
    }
    window.SetLanguage("en");
    var search = window.FindControl<TextBox>("CharacterSearch")!;
    search.Text = "missing";
    Dispatcher.UIThread.RunJobs();
    Check(window.FindControl<ListBox>("CharacterList")!.ItemCount == 0, "Character search did not filter.");
    search.Text = "";
    Dispatcher.UIThread.RunJobs();
    window.FindControl<Button>("MaxAPButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetCharacter(1).AP == CharacterRecord.MaximumAP
        && window.Session.Document.GetCharacter(2).AP == 0, "Single-character AP maximum affected another member.");
    window.FindControl<Button>("MaxReserveExperienceButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetCharacter(1).ReserveExperience == CharacterRecord.MaximumReserveExperience,
        "Reserve EXP maximum button is disconnected.");
    search.Text = "Shulk";
    window.FindControl<Button>("MaxAllAPButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.Characters.All(member => member.AP == CharacterRecord.MaximumAP),
        "Bulk AP only changed the filtered character.");
    search.Text = "";
    experienceInput.Value = 12;
    string output = Path.Combine(temporary, "edited.sav");
    Check(window.SaveTo(output), "GUI save failed.");
    Check(SaveDocument.Parse(File.ReadAllBytes(output)).Money == 123, "Saved amount differs.");
    Check(SaveDocument.Parse(File.ReadAllBytes(output)).GetCharacter(1).Experience == 12,
        "File Save did not apply the EXP draft.");
    Check(File.ReadAllBytes(source).AsSpan().SequenceEqual(original), "GUI test modified its input.");
    Check(!window.LoadSave(Path.Combine(temporary, "missing.sav")), "Missing save was accepted.");
    Check(window.Session.Document.Money == 123, "Failed open replaced the current document.");
    Check(window.SaveTo(source), "Could not save the synthetic source before closing.");
    byte[] future = new byte[SaveDocument.FileSize];
    int[] futureIds = [1, 7, 14, 15];
    future[0x152330] = 4;
    for (int index = 0; index < futureIds.Length; index++)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(future.AsSpan(0x152318 + index * 2), (ushort)futureIds[index]);
        int record = 0x152368 + (futureIds[index] - 1) * 0x138;
        BinaryPrimitives.WriteUInt32LittleEndian(future.AsSpan(record), 99);
        BinaryPrimitives.WriteUInt32LittleEndian(future.AsSpan(record + 0xec), 99);
    }
    string futurePath = Path.Combine(temporary, "bfsmeria00.sav");
    File.WriteAllBytes(futurePath, future);
    Check(window.LoadSave(futurePath), "Could not open Future Connected.");
    Check(levelInput.Items.OfType<uint>().First() == 60, "Future Connected Shulk minimum differs.");
    var characterList = window.FindControl<ListBox>("CharacterList")!;
    characterList.SelectedIndex = 2;
    Check(levelInput.Items.OfType<uint>().First() == 58, "Kino minimum differs.");
    levelInput.SelectedItem = 58u;
    experienceInput.Value = 60948;
    applyProgression.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetCharacter(14).Level == 59 && experienceInput.Value == 7,
        "Future Connected EXP threshold differs.");
    experienceInput.Value = 8;
    characterList.SelectedIndex = 3;
    Check(window.Session.Document.GetCharacter(14).Experience == 8, "Character switch discarded an EXP draft.");
    experienceInput.Value = 1.5m;
    characterList.SelectedIndex = 0;
    Check(characterList.SelectedIndex == 3, "Character switch discarded invalid EXP without warning.");
    experienceInput.Value = 0;
    Check(window.SaveTo(Path.Combine(temporary, "future-edited.sav")), "Could not save Future Connected edits.");
    Check(File.ReadAllBytes(futurePath).AsSpan().SequenceEqual(future), "Future Connected input was overwritten.");
    byte[] beforeArtEditing = File.ReadAllBytes(source);
    Check(window.LoadSave(source), "Could not reload the art fixture.");
    window.SetLanguage("en");
    window.ShowArts();
    Dispatcher.UIThread.RunJobs();
    var artList = window.FindControl<ListBox>("ArtList")!;
    var artLevel = window.FindControl<ComboBox>("ArtLevelInput")!;
    var maxArt = window.FindControl<Button>("MaxArtButton")!;
    var maxArts = window.FindControl<Button>("MaxCharacterArtsButton")!;
    var learnArt = window.FindControl<Button>("LearnArtButton")!;
    var learnAllArts = window.FindControl<Button>("LearnMaxAllArtsButton")!;
    Check(window.FindControl<Grid>("ArtsPanel")!.IsVisible, "Arts tab is disconnected.");
    Check(artList.ItemsPanel.Build() is VirtualizingStackPanel { CacheLength: 1 }, "Art list has no render buffer.");
    Check(artLevel.SelectedItem is 3, "Art selection does not prefer an upgradeable art.");
    levelInput.SelectedItem = 1u;
    experienceInput.Value = 101;
    artLevel.SelectedItem = 8;
    Check(window.Session.Document.GetCharacter(1).GetArt(12).Level == 8, "Art level input is disconnected.");
    Check(levelInput.SelectedItem is 3u && experienceInput.Value == 1,
        "Art editing did not refresh committed General progression fields.");
    maxArt.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetCharacter(1).GetArt(12).Level == 12, "Single art maximum is disconnected.");
    var artSearch = window.FindControl<TextBox>("ArtSearch")!;
    artSearch.Text = "Battle Soul";
    Dispatcher.UIThread.RunJobs();
    Check(learnArt.IsVisible && learnArt.IsEnabled && !maxArt.IsVisible && !artLevel.IsVisible,
        "Missing ordinary art exposes the wrong action.");
    learnArt.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetCharacter(1).GetArt(17).Level == 1 && !learnArt.IsVisible
        && maxArt.IsVisible && artLevel.IsVisible, "Single art learning is disconnected.");
    artSearch.Text = "Shield";
    Dispatcher.UIThread.RunJobs();
    Check(!learnArt.IsVisible && !maxArt.IsVisible
        && window.FindControl<TextBlock>("ArtStatusValue")!.Text == UiLanguage.Get("ArtEventLocked"),
        "Missing Monado art can bypass a story event.");
    artSearch.Text = "no matching art";
    Dispatcher.UIThread.RunJobs();
    maxArts.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetCharacter(1).GetArt(11).Level == 12, "Bulk art maximum is disconnected.");
    artSearch.Text = "";
    Dispatcher.UIThread.RunJobs();
    Check(window.Session.Document.GetCharacter(1).GetArt(17).Level == 12
        && window.Session.Document.GetCharacter(1).GetArt(14).Level == 12, "GUI character learning missed ordinary arts.");
    Check(!window.Session.Document.GetCharacter(1).GetArt(5).Learned, "GUI character learning bypassed a story event.");
    artList.SelectedIndex = 0;
    Check(!artLevel.IsEnabled && !maxArt.IsEnabled, "Fixed talent art is editable.");
    Check(!artLevel.IsVisible && window.FindControl<TextBlock>("ArtLevelValue")!.IsVisible,
        "Readonly talent level is shown as an input.");
    foreach (string language in UiLanguage.Languages.Keys)
    {
        window.SetLanguage("zh-Hans");
        window.SetLanguage(language);
        Dispatcher.UIThread.RunJobs();
        Check(maxArts.Content?.ToString() == UiLanguage.Get("LearnMaxCharacterArts")
            && learnAllArts.Content?.ToString() == UiLanguage.Get("LearnMaxAllArts")
            && learnArt.Content?.ToString() == UiLanguage.Get("LearnArt"), "Art button translation is stale.");
        Check(window.FindControl<TextBlock>("ArtStatusValue")!.Text == UiLanguage.Get("FixedTalent"), "Art status translation is stale.");
        VerifyPageSpacing();
    }
    window.Width = 860;
    window.Height = 600;
    VerifyPageSpacing();
    window.Width = 1120;
    window.Height = 780;
    VerifyPageSpacing();
    window.SetLanguage("en");
    artSearch.Text = "no matching art";
    byte[] beforeGlobalArts = window.Session.Document.Serialize();
    learnAllArts.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(learnAllArts.IsVisible && window.Session.Document.Characters.SelectMany(character => character.Arts)
        .Where(art => art.IsLevelLearned).All(art => art.Level == art.MaximumLevel),
        "Global art learning was limited by search or selection.");
    Check(window.Session.Document.Serialize().AsSpan(0, 0x1536e8).SequenceEqual(beforeGlobalArts.AsSpan(0, 0x1536e8)),
        "Global GUI art learning altered unrelated fields.");
    artSearch.Text = "";
    Check(window.SaveTo(Path.Combine(temporary, "arts-edited.sav")), "Could not save GUI art edits.");
    var artsSaved = SaveDocument.Parse(File.ReadAllBytes(Path.Combine(temporary, "arts-edited.sav")));
    Check(artsSaved.GetCharacter(1).GetArt(12).Level == 12, "GUI art edit was not persisted.");
    Check(File.ReadAllBytes(source).AsSpan().SequenceEqual(beforeArtEditing), "GUI art edit overwrote the input save.");
    byte[] eventArtsFixture = (byte[])original.Clone();
    BinaryPrimitives.WriteUInt32LittleEndian(eventArtsFixture, 7);
    BinaryPrimitives.WriteUInt16LittleEndian(eventArtsFixture.AsSpan(0x15231c), 7);
    BinaryPrimitives.WriteUInt16LittleEndian(eventArtsFixture.AsSpan(0x15231e), 8);
    eventArtsFixture[0x152330] = 4;
    foreach (int owner in new[] { 7, 8 })
        BinaryPrimitives.WriteUInt32LittleEndian(eventArtsFixture.AsSpan(0x152368 + (owner - 1) * 0x138), 99);
    foreach (int id in new[] { 118, 143 }) eventArtsFixture[0x1536e8 + (id - 1) * 2 + 1] = 0x80;
    string eventArtsPath = Path.Combine(temporary, "event-arts.sav");
    File.WriteAllBytes(eventArtsPath, eventArtsFixture);
    foreach ((int characterIndex, int id, string name) in new[] { (2, 118, "Mind Blast"), (3, 143, "Final Cross") })
    {
        Check(window.LoadSave(eventArtsPath), "Could not open the event-art fixture.");
        window.ShowArts();
        characterList.SelectedIndex = characterIndex;
        artSearch.Text = name;
        Dispatcher.UIThread.RunJobs();
        Check(learnArt.IsVisible && learnArt.IsEnabled && !artLevel.IsVisible
            && window.FindControl<TextBlock>("ArtStatusValue")!.Text == UiLanguage.Get("NotLearned"),
            "Supported event art still appears blocked.");
        learnArt.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        byte[] expectedEvent = (byte[])eventArtsFixture.Clone();
        int eventOffset = 0x1536e8 + (id - 1) * 2;
        expectedEvent[eventOffset] = 1;
        Check(window.Session!.Document.Serialize().AsSpan().SequenceEqual(expectedEvent)
            && !learnArt.IsVisible && artLevel.IsVisible && maxArt.IsEnabled,
            "GUI event learning changed unrelated state or failed to refresh.");
        maxArt.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        expectedEvent[eventOffset] = 12;
        expectedEvent[eventOffset + 1] = 0x87;
        string eventOutput = Path.Combine(temporary, $"event-{id}-edited.sav");
        Check(window.SaveTo(eventOutput) && File.ReadAllBytes(eventOutput).AsSpan().SequenceEqual(expectedEvent),
            "GUI event-art maximum was not saved with isolated bytes.");
        Check(File.ReadAllBytes(eventArtsPath).AsSpan().SequenceEqual(eventArtsFixture),
            "Event-art GUI editing overwrote its input.");
    }
    artSearch.Text = "";
    byte[] linksFixture = SkillLinksTests.Fixture((byte[])original.Clone());
    string linksPath = Path.Combine(temporary, "links.sav");
    File.WriteAllBytes(linksPath, linksFixture);
    Check(window.LoadSave(linksPath), "Could not open the skill-link fixture.");
    characterList.SelectedIndex = 0;
    window.ShowSkillLinks();
    VerifyPageSpacing();
    Check(window.FindControl<ItemsControl>("SkillLinkList")!.ItemCount == 6
        && window.FindControl<Grid>("SkillLinksPanel")!.IsVisible,
        "Skill links are not grouped by all available source characters.");
    Dispatcher.UIThread.RunJobs();
    var linkInput = window.GetVisualDescendants().OfType<ComboBox>().First(control => control.Name == "SkillLinkInput");
    linkInput.SelectedIndex = 1;
    int firstLinkId = window.Session!.Document.GetCharacter(1).GetSkillLink(2, 1).SkillId;
    Check(firstLinkId > 0 && window.Session.HasChanges, "Selecting a skill did not update its link.");
    byte[] linkedBytes = window.Session.Document.Serialize();
    foreach (var language in UiLanguage.Languages)
    {
        window.SetLanguage(language.Key);
        Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(linkedBytes), "Language switching changed skill links.");
        Check(window.FindControl<Grid>("SkillLinksPanel")!.IsVisible
            && window.FindControl<ItemsControl>("SkillLinkList")!.ItemCount == 6, "Language switching lost link groups.");
        VerifyPageSpacing();
    }
    window.SetLanguage("en");
    string linksOutput = Path.Combine(temporary, "links-edited.sav");
    Check(window.SaveTo(linksOutput), "Could not save GUI skill links.");
    Check(File.ReadAllBytes(linksOutput).AsSpan().SequenceEqual(linkedBytes), "GUI skill links were not persisted.");
    Dispatcher.UIThread.RunJobs();
    linkInput = window.GetVisualDescendants().OfType<ComboBox>().First(control => control.Name == "SkillLinkInput");
    linkInput.SelectedIndex = 0;
    Check(window.Session.Document.GetCharacter(1).GetSkillLink(2, 1).SkillId == 0
        && window.Session.Document.Serialize().AsSpan().SequenceEqual(linksFixture), "GUI link removal changed unrelated data.");
    Check(File.ReadAllBytes(linksPath).AsSpan().SequenceEqual(linksFixture), "GUI link editing overwrote its source.");

    byte[] affinityFixture = AffinityTests.Fixture((byte[])original.Clone());
    string affinityPath = Path.Combine(temporary, "affinity.sav");
    File.WriteAllBytes(affinityPath, affinityFixture);
    Check(window.LoadSave(affinityPath), "Could not open the affinity fixture.");
    window.ShowAffinity();
    VerifyPageSpacing();
    var affinityList = window.FindControl<ListBox>("AffinityList")!;
    var affinityPoints = window.FindControl<NumericUpDown>("AffinityPointsInput")!;
    var affinitySearch = window.FindControl<TextBox>("AffinitySearch")!;
    var maxAffinity = window.FindControl<Button>("MaxAffinityButton")!;
    var maxAllAffinity = window.FindControl<Button>("MaxAllAffinityButton")!;
    Check(affinityList.ItemCount == 21 && !window.Session!.HasChanges && affinityPoints.Value == 0,
        "Affinity inspection changes bytes or duplicates Fiora pairs.");
    affinityPoints.Value = 2_000;
    var affinityPair = window.Session!.Document.GetAffinity(1, 2);
    Check(affinityPair.Points == 2_000 && affinityPair.FirstUnlockedSlots == 3 && affinityPair.SecondUnlockedSlots == 3,
        "GUI affinity editing does not update both directed skill-link unlocks.");
    byte[] affinityEdited = window.Session.Document.Serialize();
    affinityPoints.Value = 1.5m;
    Check(!window.SaveTo(Path.Combine(temporary, "invalid-affinity.sav"))
        && window.Session.Document.Serialize().AsSpan().SequenceEqual(affinityEdited), "Fractional affinity was saved or mutated the document.");
    window.SetLanguage("ja");
    Check(UiLanguage.Current == "en", "Language switching discarded an invalid affinity draft.");
    affinityPoints.Value = 2_000;
    maxAffinity.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(affinityPair.IsMaximum, "Single-pair maximum is disconnected.");
    affinityPoints.Value = 0;
    Check(affinityPair.Points == 0 && affinityPair.FirstUnlockedSlots == 5, "GUI lowering relocked skill links.");
    affinitySearch.Text = "Shulk — Reyn";
    Dispatcher.UIThread.RunJobs();
    Check(affinityList.ItemCount == 1, "Affinity search did not filter pair names.");
    maxAllAffinity.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.Affinities.All(pair => pair.IsMaximum) && !maxAllAffinity.IsEnabled,
        "Bulk affinity maximum was limited to search results.");
    affinitySearch.Text = "";
    Dispatcher.UIThread.RunJobs();
    byte[] affinityMaximum = window.Session.Document.Serialize();
    foreach (var language in UiLanguage.Languages)
    {
        window.SetLanguage(language.Key);
        Check(affinityList.ItemCount == 21 && window.FindControl<Grid>("AffinityPanel")!.IsVisible
            && window.Session.Document.Serialize().AsSpan().SequenceEqual(affinityMaximum), "Language switching changed affinity or lost pairs.");
        VerifyPageSpacing();
    }
    window.Width = 860;
    window.Height = 600;
    VerifyPageSpacing();
    window.Width = 1120;
    window.Height = 780;
    VerifyPageSpacing();
    window.SetLanguage("en");
    string affinityOutput = Path.Combine(temporary, "affinity-edited.sav");
    Check(window.SaveTo(affinityOutput) && File.ReadAllBytes(affinityOutput).AsSpan().SequenceEqual(affinityMaximum),
        "GUI affinity changes were not persisted.");
    Check(File.ReadAllBytes(affinityPath).AsSpan().SequenceEqual(affinityFixture), "GUI affinity changed its source save.");
    byte[] protectedAffinity = (byte[])affinityFixture.Clone();
    BinaryPrimitives.WriteUInt32LittleEndian(protectedAffinity, 8);
    string protectedAffinityPath = Path.Combine(temporary, "affinity-version8.sav");
    File.WriteAllBytes(protectedAffinityPath, protectedAffinity);
    Check(window.LoadSave(protectedAffinityPath) && !affinityPoints.IsVisible
        && window.FindControl<TextBlock>("AffinityPointsValue")!.IsVisible
        && !maxAffinity.IsEnabled && !maxAllAffinity.IsEnabled && !window.Session!.HasChanges,
        "An unverified format shows editable affinity fields or changes bytes.");

    byte[] skillsFixture = (byte[])original.Clone();
    BinaryPrimitives.WriteUInt32LittleEndian(skillsFixture, 7);
    BinaryPrimitives.WriteUInt32LittleEndian(skillsFixture.AsSpan(0x152368 + 0x90), 1);
    BinaryPrimitives.WriteUInt32LittleEndian(skillsFixture.AsSpan(0x1524a0 + 0x90), 1);
    BinaryPrimitives.WriteUInt32LittleEndian(skillsFixture.AsSpan(0x152368 + 0x98), 5);
    BinaryPrimitives.WriteUInt32LittleEndian(skillsFixture.AsSpan(0x152368 + 0x84), uint.MaxValue);
    string skillsPath = Path.Combine(temporary, "skills.sav");
    File.WriteAllBytes(skillsPath, skillsFixture);
    Check(window.LoadSave(skillsPath), "Could not open the skill fixture.");
    characterList.SelectedIndex = 0;
    window.ShowSkills();
    VerifyPageSpacing();
    var maxAllSkills = window.FindControl<Button>("MaxAllSkillsButton")!;
    Check(window.FindControl<TabStrip>("CharacterNavigation")!.SelectedIndex == 2
        && window.FindControl<Grid>("SkillsPanel")!.IsVisible && maxAllSkills.IsVisible,
        "Skills navigation or bulk button is disconnected.");
    Check(window.FindControl<ScrollViewer>("SkillsScroll") is not null
        && window.FindControl<ItemsControl>("SkillTreeList")!.ItemCount == 5,
        "Skills do not share one panel containing all five trees.");
    Check(!window.Session!.HasChanges, "Opening Skills changed existing counts or SP.");
    Check(SkillControl<ComboBox>("SkillLearnedCountInput").Items.OfType<int>().SequenceEqual([1, 2, 3, 4, 5])
        && SkillControl<ComboBox>("SkillLearnedCountInput", 2).Items.OfType<int>().SequenceEqual([0, 1, 2, 3, 4, 5]),
        "Skill count choices ignore the innate skill minimum.");
    Check(!SkillControl<NumericUpDown>("SkillProgressInput", 3).IsVisible
        && window.Session.Document.GetCharacter(1).GetSkillTree(3).Progress == uint.MaxValue,
        "Fully learned SP is editable or existing excess SP was lost.");
    Check(!SkillControl<Button>("MaxTreeButton", 4).IsEnabled
        && !SkillControl<ComboBox>("SkillLearnedCountInput", 4).IsVisible,
        "A locked skill tree is editable.");
    Check(SkillControl<Button>("UnlockTreeButton", 4).IsVisible
        && !SkillControl<Button>("UnlockTreeButton").IsVisible,
        "Single unlock is not limited to locked hidden branches.");
    byte[] beforeTreeUnlock = window.Session.Document.Serialize();
    SkillControl<Button>("UnlockTreeButton", 4).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    byte[] expectedTreeUnlock = (byte[])beforeTreeUnlock.Clone();
    int unlockBit = 0x2cdd + 1;
    expectedTreeUnlock[0x50 + (unlockBit >> 3)] |= (byte)(1 << (unlockBit & 7));
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(expectedTreeUnlock)
        && SkillControl<ComboBox>("SkillLearnedCountInput", 4).IsVisible
        && !SkillControl<Button>("UnlockTreeButton", 4).IsVisible,
        "Single GUI unlock changed quest/progress bytes or failed to refresh the card.");
    var firstTree = window.Session.Document.GetCharacter(1).GetSkillTree(1);
    Check(SkillControl<NumericUpDown>("SkillProgressInput").Maximum == firstTree.MaximumProgress,
        "Skill SP limit differs from the next-node residual cap.");
    SkillControl<NumericUpDown>("SkillProgressInput").Value = 12;
    Check(firstTree.Progress == 0, "Typing skill SP committed a partial number.");
    SkillControl<Button>("ApplySkillProgressButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(firstTree.Progress == 12 && firstTree.LearnedCount == 1, "Applying residual SP changed the learned count.");
    SkillControl<NumericUpDown>("SkillProgressInput").Value = 1.5m;
    SkillControl<ComboBox>("SkillLearnedCountInput").SelectedItem = 2;
    Check(firstTree.LearnedCount == 2 && firstTree.Progress == 0
        && SkillControl<NumericUpDown>("SkillProgressInput").Value == 0,
        "Selecting a learned count failed to reset SP and clear its draft.");
    SkillControl<NumericUpDown>("SkillProgressInput").Value = 24;
    SkillControl<NumericUpDown>("SkillProgressInput", 2).Value = 1.5m;
    SkillControl<Button>("ApplySkillProgressButton", 2).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetCharacter(1).GetSkillTree(2).Progress == 0,
        "Fractional skill SP was applied.");
    byte[] beforeSkillSave = window.Session.Document.Serialize();
    string invalidSkillsPath = Path.Combine(temporary, "invalid-skills.sav");
    Check(!window.SaveTo(invalidSkillsPath) && !File.Exists(invalidSkillsPath)
        && window.Session.Document.Serialize().AsSpan().SequenceEqual(beforeSkillSave),
        "Invalid skill drafts did not block File Save atomically.");
    window.SetLanguage("ja");
    characterList.SelectedIndex = 1;
    SkillControl<NumericUpDown>("SkillProgressInput").Value = 8;
    characterList.SelectedIndex = 0;
    Check(SkillControl<NumericUpDown>("SkillProgressInput").Value == 24
        && SkillControl<NumericUpDown>("SkillProgressInput", 2).Value == 1.5m,
        "Language or character switching discarded skill drafts.");
    Check(!window.SaveTo(invalidSkillsPath)
        && window.Session.Document.GetCharacter(2).GetSkillTree(1).Progress == 0,
        "An invalid draft allowed another character's valid SP draft to commit.");
    SkillControl<NumericUpDown>("SkillProgressInput", 2).Value = 9;
    string skillsOutput = Path.Combine(temporary, "skills-edited.sav");
    Check(window.SaveTo(skillsOutput), "File Save did not commit valid skill drafts.");
    var skillsSaved = SaveDocument.Parse(File.ReadAllBytes(skillsOutput));
    Check(skillsSaved.GetCharacter(1).GetSkillTree(1).Progress == 24
        && skillsSaved.GetCharacter(1).GetSkillTree(2).Progress == 9
        && skillsSaved.GetCharacter(2).GetSkillTree(1).Progress == 8,
        "File Save omitted a skill draft from the current or another character.");
    SkillControl<NumericUpDown>("SkillProgressInput").Value = 1.5m;
    SkillControl<Button>("MaxTreeButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(firstTree.LearnedCount == 5 && firstTree.Progress == 0
        && window.SaveTo(skillsOutput), "Max Tree did not clear its invalid SP draft.");
    SkillControl<NumericUpDown>("SkillProgressInput", 2).Value = 1.5m;
    window.FindControl<Button>("MaxCharacterSkillsButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetCharacter(1).SkillTrees.Where(tree => tree.CanEdit)
        .All(tree => tree.LearnedCount == 5 && tree.Progress == 0) && window.SaveTo(skillsOutput),
        "Max Character Skills did not normalize its trees and clear invalid drafts.");
    characterList.SelectedIndex = 1;
    SkillControl<NumericUpDown>("SkillProgressInput").Value = 1.5m;
    search.Text = "Shulk";
    maxAllSkills.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.Characters.SelectMany(character => character.SkillTrees)
        .Where(tree => tree.CanEdit).All(tree => tree.LearnedCount == 5 && tree.Progress == 0)
        && window.SaveTo(skillsOutput), "Max All Skills respected filtering or retained an invalid draft.");
    Check(window.Session.Document.Characters.SelectMany(character => character.SkillTrees)
        .All(tree => tree.IsUnlocked && tree.LearnedCount == 5 && tree.Progress == 0),
        "Explicit unlock-and-learn batch left hidden branches locked or unlearned.");
    search.Text = "";
    foreach (string language in UiLanguage.Languages.Keys)
    {
        window.SetLanguage(language);
        VerifyPageSpacing();
        Check(maxAllSkills.Content?.ToString() == UiLanguage.Get("UnlockMaxAllSkills")
            && window.FindControl<Button>("MaxCharacterSkillsButton")!.Content?.ToString() == UiLanguage.Get("UnlockMaxCharacterSkills"),
            "Skill batch button translation is stale.");
        Check(SkillControl<Button>("MaxTreeButton").Content?.ToString() == UiLanguage.Get("MaxTree"),
            "Single-branch button translation is stale.");
        Check(SkillControl<Button>("UnlockTreeButton", 4).Content?.ToString() == UiLanguage.Get("UnlockSkillBranch"),
            "Hidden-branch unlock translation is stale.");
        window.Width = 860;
        window.Height = 600;
        VerifyPageSpacing();
        var label = maxAllSkills.GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.Text == UiLanguage.Get("UnlockMaxAllSkills"));
        Check(label.TextWrapping == Avalonia.Media.TextWrapping.Wrap
            && label.Bounds.Width <= maxAllSkills.Bounds.Width,
            $"A translated batch label cannot wrap at minimum width: {language}, {label.TextWrapping}, {label.Bounds}, {maxAllSkills.Bounds}.");
        window.Width = 1120;
        window.Height = 780;
    }
    window.Width = 860;
    window.Height = 600;
    VerifyPageSpacing();
    Dispatcher.UIThread.RunJobs();
    var allSkillsText = maxAllSkills.GetVisualDescendants().OfType<TextBlock>()
        .First(text => text.Text == UiLanguage.Get("UnlockMaxAllSkills"));
    Check(allSkillsText.Bounds.Width <= maxAllSkills.Bounds.Width
        && !string.IsNullOrWhiteSpace(allSkillsText.Text), "Skill batch label is clipped at minimum width.");
    window.Width = 1120;
    window.Height = 780;
    window.ShowArts();
    VerifyPageSpacing();
    Check(!maxAllSkills.IsVisible, "Max All Skills remains visible outside Skills.");
    Check(File.ReadAllBytes(skillsPath).AsSpan().SequenceEqual(skillsFixture), "GUI skill editing overwrote its source.");
    byte[] protectedSkills = (byte[])skillsFixture.Clone();
    BinaryPrimitives.WriteUInt32LittleEndian(protectedSkills, 8);
    string protectedSkillsPath = Path.Combine(temporary, "skills-version8.sav");
    File.WriteAllBytes(protectedSkillsPath, protectedSkills);
    Check(window.LoadSave(protectedSkillsPath), "Could not inspect an unsupported skill format.");
    window.ShowSkills();
    Check(!maxAllSkills.IsEnabled && !window.FindControl<Button>("MaxCharacterSkillsButton")!.IsEnabled
        && !SkillControl<Button>("UnlockTreeButton", 4).IsVisible
        && !SkillControl<ComboBox>("SkillLearnedCountInput").IsVisible && !window.Session!.HasChanges,
        "An unverified skill format is editable or exposes unlock actions.");
    window.SetLanguage("en");
    byte[] equipmentFixture = EquipmentTests.Fixture((byte[])original.Clone());
    string equipmentPath = Path.Combine(temporary, "equipment.sav");
    File.WriteAllBytes(equipmentPath, equipmentFixture);
    Check(window.LoadSave(equipmentPath), "Could not open the equipment fixture.");
    window.ShowEquipment();
    VerifyPageSpacing();
    Check(window.FindControl<ItemsControl>("EquipmentList")!.ItemCount == 6
        && !window.Session!.HasChanges && !maxAllSkills.IsVisible,
        "Equipment navigation mutated the save or omitted a slot.");
    ComboBox GemInput(int index = 0)
    {
        Dispatcher.UIThread.RunJobs();
        return window.FindControl<ItemsControl>("EquipmentList")!.GetVisualDescendants()
            .OfType<ComboBox>().Where(input => input.Name == "EquipmentGemInput").ElementAt(index);
    }
    int? ChoiceIndex(object choice) => (int?)choice.GetType().GetProperty("Index")!.GetValue(choice);
    Check(GemInput().IsVisible && !GemInput(1).IsVisible && GemInput(2).IsVisible,
        "Fixed gem is displayed as an editable input.");
    var gemInput = GemInput();
    Check(gemInput.Items.Cast<object>().Select(ChoiceIndex).SequenceEqual(new int?[] { null, 0, 3, 4 }),
        "Gem choices include an occupied gem or cylinder.");
    gemInput.SelectedItem = gemInput.Items.Cast<object>().Single(choice => ChoiceIndex(choice) == 3);
    Check(window.Session.Document.GetCharacter(1).GetEquipment(EquipmentSlot.Weapon).GemSockets[0].GemIndex == 3,
        "Selecting a gem did not apply its reference.");
    string equipmentOutput = Path.Combine(temporary, "equipment-edited.sav");
    Check(window.SaveTo(equipmentOutput), "Could not save GUI equipment edits.");
    byte[] expectedEquipment = (byte[])equipmentFixture.Clone();
    BinaryPrimitives.WriteUInt32LittleEndian(expectedEquipment.AsSpan(EquipmentTests.Weapon + 0x18), 3u | (3u << 16));
    Check(File.ReadAllBytes(equipmentOutput).AsSpan().SequenceEqual(expectedEquipment),
        "GUI gem fitting changed more than one socket.");
    foreach (string language in UiLanguage.Languages.Keys)
    {
        window.SetLanguage(language);
        VerifyPageSpacing();
        Check(window.FindControl<TabStrip>("CharacterNavigation")!.Items.OfType<TabStripItem>().ElementAt(3)
            .Content?.ToString() == UiLanguage.Get("Equipment"), "Equipment tab translation is stale.");
        Check(window.FindControl<ItemsControl>("EquipmentList")!.GetVisualDescendants().OfType<TextBlock>()
            .Any(text => text.Text == UiLanguage.Get("FixedGem")), "Fixed gem translation is stale.");
        Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(expectedEquipment),
            "Language switching altered equipment.");
    }
    GemInput().SelectedItem = GemInput().Items.Cast<object>().Single(choice => ChoiceIndex(choice) is null);
    Check(window.Session.Document.GetCharacter(1).GetEquipment(EquipmentSlot.Weapon).GemSockets[0].IsEmpty,
        "None did not remove the normal gem.");
    Check(GemInput(2).Items.Cast<object>().Select(ChoiceIndex).Contains(3), "Removed gem was not released for another socket.");
    window.Width = 860;
    window.Height = 600;
    VerifyPageSpacing();
    window.Width = 1120;
    window.Height = 780;
    Check(File.ReadAllBytes(equipmentPath).AsSpan().SequenceEqual(equipmentFixture), "GUI equipment edit overwrote its source.");
    var switchingFixture = EquipmentSwitchTests.Fixture((byte[])original.Clone());
    string switchingPath = Path.Combine(temporary, "switching.sav");
    File.WriteAllBytes(switchingPath, switchingFixture);
    Check(window.LoadSave(switchingPath), "Could not open the switching fixture.");
    window.ShowEquipment();
    Dispatcher.UIThread.RunJobs();
    ComboBox ItemInput(int slot = 0)
    {
        Dispatcher.UIThread.RunJobs();
        return window.FindControl<ItemsControl>("EquipmentList")!.GetVisualDescendants().OfType<ComboBox>()
            .Where(input => input.Name == "EquipmentItemInput").ElementAt(slot);
    }
    Check(ItemInput().IsVisible && ItemInput().Items.Cast<object>().Select(ChoiceIndex).SequenceEqual(new int?[] { 0, 2 }),
        "GUI equipment choices include incompatible or occupied items.");
    Check(!window.Session!.HasChanges, "Opening equipment selectors changed the save.");
    ItemInput().SelectedItem = ItemInput().Items.Cast<object>().Single(choice => ChoiceIndex(choice) == 2);
    Check(window.Session.Document.GetCharacter(1).GetEquipment(EquipmentSlot.Weapon).Index == 2
        && window.Session.Document.GetCharacter(1).GetEquipment(EquipmentSlot.Weapon).GemSockets[0].GemIndex == 2,
        "GUI weapon selection lost its existing gem or did not change the reference.");
    string switchingOutput = Path.Combine(temporary, "switching-edited.sav");
    Check(window.SaveTo(switchingOutput), "Could not save equipment switching.");
    var expectedSwitching = (byte[])switchingFixture.Clone();
    BinaryPrimitives.WriteUInt32LittleEndian(expectedSwitching.AsSpan(0x152368 + 0x28), 2u | (2u << 16));
    Check(File.ReadAllBytes(switchingOutput).AsSpan().SequenceEqual(expectedSwitching),
        "GUI switching changed more than the selected reference.");
    foreach (string language in UiLanguage.Languages.Keys)
    {
        window.SetLanguage(language);
        VerifyPageSpacing();
        Check(ChoiceIndex(ItemInput().SelectedItem!) == 2 && window.Session.Document.Serialize().AsSpan().SequenceEqual(expectedSwitching),
            "Language switching changed equipment selection or the save.");
    }
    window.Width = 860;
    window.Height = 600;
    VerifyPageSpacing();
    window.Width = 1120;
    window.Height = 780;
    Check(File.ReadAllBytes(switchingPath).AsSpan().SequenceEqual(switchingFixture), "GUI switching overwrote the source.");
    window.SetLanguage("en");
    byte[] ambiguous = (byte[])original.Clone();
    ambiguous[0x152330] = 1;
    string ambiguousPath = Path.Combine(temporary, "ambiguous.sav");
    File.WriteAllBytes(ambiguousPath, ambiguous);
    Check(window.LoadSave(ambiguousPath), "Could not open an ambiguous campaign.");
    Check(window.FindControl<TextBlock>("CampaignValue")!.Text == UiLanguage.Get("Unknown"),
        "An ambiguous campaign was shown as the main story.");
    Check(!window.FindControl<StackPanel>("AffinityCoinsField")!.IsVisible,
        "An ambiguous campaign exposes coin editing.");
    Check(!window.FindControl<StackPanel>("ProgressionInputs")!.IsEnabled, "Ambiguous campaign exposes level editing.");
    Check(!window.Session!.HasChanges, "Opening an ambiguous campaign altered it.");
    byte[] high = (byte[])ambiguous.Clone();
    BinaryPrimitives.WriteUInt32LittleEndian(high.AsSpan(0x152370), uint.MaxValue);
    BinaryPrimitives.WriteUInt32LittleEndian(high.AsSpan(0x15245c), uint.MaxValue);
    string highPath = Path.Combine(temporary, "high.sav");
    File.WriteAllBytes(highPath, high);
    Check(window.LoadSave(highPath), "Could not open existing high resource values.");
    Check(!window.Session!.HasChanges && ap.Value == uint.MaxValue && reserve.Value == uint.MaxValue,
        "Loading clamped an existing high amount.");
    ap.Value = uint.MaxValue - 1;
    Check(!window.SaveTo(Path.Combine(temporary, "invalid-high.sav")), "A new over-cap AP value was saved.");
    Check(window.Session.Document.GetCharacter(1).AP == uint.MaxValue,
        "A rejected over-cap AP draft mutated the document.");
    ap.Value = uint.MaxValue;
    reserve.Value = 123;
    Check(window.Session.Document.GetCharacter(1).AP == uint.MaxValue && window.Session.Document.GetCharacter(1).ReserveExperience == 123,
        "Reserve EXP edit rejected or normalized unchanged high AP.");
    Check(window.SaveTo(highPath), "Could not save high-value fixture.");
    Check(window.LoadSave(equipmentPath), "Could not load gem inventory.");
    window.ShowGems();
    VerifyPageSpacing();
    var gemList = window.FindControl<ListBox>("GemList")!;
    var gemEffect = window.FindControl<ComboBox>("GemEffectInput")!;
    var gemRank = window.FindControl<ComboBox>("GemRankInput")!;
    var gemStrength = window.FindControl<NumericUpDown>("GemStrengthInput")!;
    var gemMax = window.FindControl<Button>("MaxGemButton")!;
    var gemApply = window.FindControl<Button>("ApplyGemButton")!;
    Check(gemList.ItemCount == 5 && gemEffect.Items.Count == window.Session!.Document.GetGem(0).AvailableDefinitions.DistinctBy(rule => rule.EffectId).Count() && !window.Session.HasChanges,
        "Gem inventory omitted effects, included cylinders or changed bytes on load.");
    Check(gemStrength.Minimum == 75 && gemStrength.Maximum == 100 && gemStrength.Value == 100,
        "Gem bounds do not match the selected effect and rank.");
    object EffectChoice(int id) => gemEffect.Items.Cast<object>().Single(choice =>
        (int)choice.GetType().GetProperty("Id")!.GetValue(choice)! == id);
    gemEffect.SelectedItem = EffectChoice(26);
    Check(gemStrength.Value == 150 && gemStrength.Minimum == 150 && gemStrength.Maximum == 200
        && window.FindControl<TextBlock>("GemChanceValue")!.Text == "25%" && !window.Session.HasChanges,
        "Changing gem effect failed linked bounds or committed a draft.");
    gemMax.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetGem(0).Value == 6600, "GUI maximum did not encode HP Steal.");
    gemStrength.Value = 150.5m;
    gemApply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetGem(0).Value == 6600, "GUI committed a fractional gem value.");
    string invalidGemPath = Path.Combine(temporary, "invalid-gem.sav");
    Check(!window.SaveTo(invalidGemPath) && !File.Exists(invalidGemPath), "Invalid gem draft was saved.");
    string previousLanguage = UiLanguage.Current;
    window.SetLanguage("ja");
    Check(UiLanguage.Current == previousLanguage, "Language switching discarded an invalid gem draft.");
    gemList.SelectedIndex = 1;
    Check(gemList.SelectedIndex == 0, "Selection discarded an invalid gem draft.");
    gemStrength.Value = 180;
    window.SetLanguage("zh-Hans");
    Check(window.Session.Document.GetGem(0).Strength == 180, "Language switching lost a valid gem draft.");
    var gemBytes = window.Session.Document.Serialize();
    foreach (string language in UiLanguage.Languages.Keys)
    {
        window.SetLanguage(language);
        Check(window.FindControl<TabStrip>("MainNavigation")!.Items.OfType<TabStripItem>().ElementAt(2)
            .Content?.ToString() == UiLanguage.Get("Items") && gemMax.Content?.ToString() == UiLanguage.Get("MaxGem"),
            "Gem UI translation is stale.");
        Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(gemBytes), "Gem language switching changed bytes.");
    }
    gemRank.SelectedIndex = 0;
    Check(gemStrength.Minimum == 10 && gemStrength.Maximum == 20 && gemStrength.Value == 20
        && window.FindControl<TextBlock>("GemChanceValue")!.Text == "5%", "Gem rank linkage differs.");
    gemApply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetGem(0).Value == 1300 && window.Session.Document.GetGem(0).Rank == 1,
        "GUI rank edit did not encode both strength and chance.");
    gemEffect.SelectedItem = EffectChoice(39);
    Check(!window.FindControl<StackPanel>("GemStrengthField")!.IsVisible && !gemMax.IsVisible,
        "Chance-only gem has a meaningless value input or maximum button.");
    gemApply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetGem(0).Strength == 0 && window.Session.Document.GetGem(0).Chance == 5,
        "Chance-only gem edit is invalid.");
    string gemOutput = Path.Combine(temporary, "gems-output.sav");
    Check(window.SaveTo(gemOutput), "Could not save edited gems.");
    Check(File.ReadAllBytes(equipmentPath).AsSpan().SequenceEqual(equipmentFixture), "GUI gem edit changed its source.");
    byte[] unusualGem = (byte[])equipmentFixture.Clone();
    BinaryPrimitives.WriteUInt16LittleEndian(unusualGem.AsSpan(EquipmentTests.Gem + 0x1c), 17);
    string unusualGemPath = Path.Combine(temporary, "unusual-gem.sav");
    File.WriteAllBytes(unusualGemPath, unusualGem);
    Check(window.LoadSave(unusualGemPath) && gemStrength.Value == 100 && gemStrength.Maximum == 100,
        "An existing over-limit gem value was clamped on load.");
    window.SetLanguage("en");
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(unusualGem), "Inspection normalized an unusual gem.");
    gemMax.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetGem(0).Strength == 50, "Explicit maximum did not repair over-limit HP Up.");
    window.Width = 860;
    window.Height = 600;
    VerifyPageSpacing();
    window.Width = 1120;
    window.Height = 780;
    byte[] inventoryFixture = InventoryTests.Fixture(EquipmentTests.Fixture(original));
    string inventoryPath = Path.Combine(temporary, "inventory.sav");
    File.WriteAllBytes(inventoryPath, inventoryFixture);
    Check(window.LoadSave(inventoryPath), "Could not load inventory fixture.");
    window.ShowInventory(InventoryKind.Collectables);
    var itemTabs = window.FindControl<TabStrip>("ItemNavigation")!;
    var itemList = window.FindControl<ListBox>("InventoryList")!;
    var addItem = window.FindControl<Button>("CreateItem")!;
    var newQuantity = window.FindControl<NumericUpDown>("NewItemQuantity")!;
    var quantity = window.FindControl<NumericUpDown>("InventoryQuantityInput")!;
    var favorite = window.FindControl<CheckBox>("InventoryFavoriteInput")!;
    Check(itemTabs.SelectedIndex == 1 && itemList.ItemCount == 0 && !window.Session!.HasChanges,
        "Inventory inspection changes bytes or displays the wrong category.");
    Check(window.FindControl<Control>("CreateItemCard")!.IsVisible && addItem.IsEffectivelyEnabled
        && !window.FindControl<Control>("InventoryEditorCard")!.IsVisible && window.OwnedWindows.Count == 0,
        "Empty inventory blocks inline creation or exposes an unrelated editor.");
    newQuantity.Value = 1.5m;
    addItem.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(itemList.ItemCount == 0 && window.OwnedWindows.Count == 0 && !window.Session!.HasChanges,
        "Creation committed a fractional quantity.");
    newQuantity.Value = 1;
    string creationDraftOutput = Path.Combine(temporary, "creation-draft.sav");
    Check(window.SaveTo(creationDraftOutput) && File.ReadAllBytes(creationDraftOutput).AsSpan().SequenceEqual(inventoryFixture),
        "Saving a selected creation definition silently added an item.");
    addItem.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Dispatcher.UIThread.RunJobs();
    Check(itemList.ItemCount == 1 && quantity.Value == 1, "GUI failed to create a stack.");
    quantity.Value = 2;
    favorite.IsChecked = true;
    window.FindControl<Button>("ApplyInventoryButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    var stack = window.Session.Document.Inventory(InventoryKind.Collectables).Single();
    Check(stack.Quantity == 2 && stack.Favorite, "GUI quantity/favorite edit failed.");
    quantity.Value = 2.5m;
    byte[] invalidDraftBytes = window.Session.Document.Serialize();
    addItem.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(invalidDraftBytes),
        "Inline creation ignored an invalid selected-item draft.");
    itemTabs.SelectedIndex = 2;
    Check(itemTabs.SelectedIndex == 1 && stack.Quantity == 2, "Invalid item draft was discarded by tab switching.");
    string invalidItemPath = Path.Combine(temporary, "invalid-item.sav");
    Check(!window.SaveTo(invalidItemPath) && !File.Exists(invalidItemPath), "Invalid inventory draft was saved.");
    quantity.Value = 3;
    window.SetLanguage("ja");
    Check(stack.Quantity == 3, "Language switching lost a valid inventory draft.");
    var inventoryBytes = window.Session.Document.Serialize();
    foreach (string language in UiLanguage.Languages.Keys)
    {
        window.SetLanguage(language);
        Check(window.FindControl<TextBlock>("InventoryTitle")!.Text == UiLanguage.Get("Collectables")
            && addItem.Content?.ToString() == UiLanguage.Get("Add"), "Inventory UI translation is stale.");
        Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(inventoryBytes), "Inventory language switch changed bytes.");
    }
    window.FindControl<Button>("MaxInventoryButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(stack.Quantity == 99 && quantity.Value == 99, "GUI bulk quantity maximum failed.");
    window.FindControl<Button>("DeleteInventoryButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(itemList.ItemCount == 0 && !stack.Exists, "GUI deletion failed.");
    window.ShowInventory(InventoryKind.KeyItems);
    Check(!window.FindControl<StackPanel>("InventoryActions")!.IsVisible
        && !window.FindControl<Control>("CreateItemCard")!.IsVisible && !addItem.IsEnabled,
        "Quest items expose mutation controls.");
    window.ShowInventory(InventoryKind.ArtManuals);
    var itemSearch = window.FindControl<TextBox>("NewItemSearch")!;
    var catalogItems = window.FindControl<ComboBox>("NewItemDefinition")!;
    itemSearch.Text = "(Master)";
    Dispatcher.UIThread.RunJobs();
    Check(catalogItems.ItemCount > 0, "Manual tier search found no books.");
    itemSearch.Text = "no matching manual";
    Dispatcher.UIThread.RunJobs();
    Check(catalogItems.ItemCount == 0 && !addItem.IsEnabled,
        "Empty creation search retains a hidden selection.");
    Check(window.Session.Document.Inventory(InventoryKind.ArtManuals).Count == 0, "Searching creation definitions added an item.");
    window.ShowGems();
    Check(itemTabs.SelectedIndex == 0 && !window.FindControl<Button>("DeleteGemButton")!.IsEnabled,
        "Gems navigation or equipped-gem deletion guard failed.");
    var newGemStrength = window.FindControl<NumericUpDown>("NewGemStrength")!;
    var newGemEffect = window.FindControl<ComboBox>("NewGemEffect")!;
    var createGem = window.FindControl<Button>("CreateGem")!;
    Check(newGemStrength.Maximum == 100 && createGem.IsEffectivelyEnabled && window.OwnedWindows.Count == 0,
        "Inline gem creation does not use linked limits.");
    byte[] beforeGemCreation = window.Session.Document.Serialize();
    newGemStrength.Value = 99.5m;
    createGem.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(beforeGemCreation),
        "Gem creation committed a fractional value.");
    newGemStrength.Value = 100;
    createGem.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Dispatcher.UIThread.RunJobs();
    Check(window.OwnedWindows.Count == 0 && gemList.ItemCount == 6
        && window.FindControl<Button>("DeleteGemButton")!.IsEnabled, "GUI gem creation or selection failed.");
    window.FindControl<Button>("DeleteGemButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(gemList.ItemCount == 5, "GUI unreferenced gem deletion failed.");
    newGemEffect.SelectedItem = newGemEffect.Items.Cast<object>().Single(choice =>
        (int)choice.GetType().GetProperty("Id")!.GetValue(choice)! == 39);
    Check(!window.FindControl<StackPanel>("NewGemStrengthField")!.IsVisible
        && window.FindControl<StackPanel>("NewGemChanceField")!.IsVisible
        && newGemStrength.Value == 0, "Chance-only gem creation exposes a meaningless strength input.");
    beforeGemCreation = window.Session.Document.Serialize();
    window.SetLanguage("ja");
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(beforeGemCreation)
        && newGemStrength.Value == 0 && createGem.Content?.ToString() == UiLanguage.Get("Add"),
        "Language switching committed or reset an independent gem creation draft.");
    window.ShowInventory(InventoryKind.Materials);
    window.Width = 860;
    window.Height = 600;
    VerifyPageSpacing();
    void VerifyCreationCards(string editorName, string creationName, string scrollName, string buttonName)
    {
        Dispatcher.UIThread.RunJobs();
        var editor = window.FindControl<Control>(editorName)!;
        var creation = window.FindControl<Control>(creationName)!;
        var scroll = window.FindControl<ScrollViewer>(scrollName)!;
        var button = window.FindControl<Button>(buttonName)!;
        Check(creation.IsVisible && creation.GetVisualAncestors().OfType<ScrollViewer>().Single() == scroll
            && creation.GetVisualDescendants().OfType<ScrollViewer>().All(view => view.Extent.Height <= view.Viewport.Height + 0.1),
            "Creation card has independent scrolling instead of sharing the right column.");
        if (editor.IsVisible)
            Check(Math.Abs(creation.Bounds.Top - editor.Bounds.Bottom - 24) < 0.1,
                "Selected-item editing and creation cards have incorrect spacing.");
        var position = button.TranslatePoint(new Point(0, 0), creation)!.Value;
        Check(position.X >= 20 && position.X + button.Bounds.Width <= creation.Bounds.Width - 19.9,
            "Inline creation controls are clipped horizontally at minimum window size.");
        scroll.Offset = new Vector(0, scroll.Extent.Height);
        Dispatcher.UIThread.RunJobs();
        position = button.TranslatePoint(new Point(0, 0), scroll)!.Value;
        Check(position.Y >= 0 && position.Y + button.Bounds.Height <= scroll.Bounds.Height + 0.1,
            "Inline Add button cannot be reached by shared scrolling.");
        scroll.Offset = new Vector(0, 0);
    }
    VerifyCreationCards("InventoryEditorCard", "CreateItemCard", "InventoryCardsScroll", "CreateItem");
    var inventoryActions = window.FindControl<StackPanel>("InventoryActions")!;
    Check(!inventoryActions.GetVisualDescendants().Contains(addItem)
        && inventoryActions.GetVisualDescendants().Contains(window.FindControl<Button>("DeleteInventoryButton")!)
        && window.FindControl<Control>("CreateItemCard")!.GetVisualDescendants().Contains(addItem),
        "List-wide inventory actions are not in the left card footer.");
    var stackPanel = window.FindControl<Grid>("StackItemsPanel")!;
    var footerPoint = inventoryActions.TranslatePoint(new Point(0, 0), stackPanel)!.Value;
    Check(footerPoint.X >= 20 && footerPoint.X + inventoryActions.Bounds.Width <= 260.1,
        "Inventory actions escaped the left card or are clipped.");
    window.ShowGems();
    VerifyCreationCards("GemEditorCard", "CreateGemCard", "GemCardsScroll", "CreateGem");
    byte[] emptyGemFixture = (byte[])inventoryFixture.Clone();
    emptyGemFixture.AsSpan(0x2c380, 500 * 44).Clear();
    string emptyGemPath = Path.Combine(temporary, "empty-gems.sav");
    File.WriteAllBytes(emptyGemPath, emptyGemFixture);
    Check(window.LoadSave(emptyGemPath) && gemList.ItemCount == 0 && createGem.IsEffectivelyEnabled
        && !window.FindControl<Control>("GemEditorCard")!.IsVisible,
        "Empty gem inventory disables its independent creation card.");
    createGem.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(gemList.ItemCount == 1 && window.OwnedWindows.Count == 0,
        "Empty gem inventory cannot create a gem inline.");
    Check(File.ReadAllBytes(emptyGemPath).AsSpan().SequenceEqual(emptyGemFixture),
        "Inline gem creation overwrote its source file before Save.");
    window.Width = 1120;
    window.Height = 780;
    Check(File.ReadAllBytes(inventoryPath).AsSpan().SequenceEqual(inventoryFixture), "Inventory GUI tests changed the source.");
    byte[] achievementFixture = (byte[])inventoryFixture.Clone();
    BinaryPrimitives.WriteUInt32LittleEndian(achievementFixture, 7);
    achievementFixture.AsSpan(0x557, 25).Clear();
    achievementFixture.AsSpan(0xe30, 400).Clear();
    string achievementPath = Path.Combine(temporary, "achievements.sav");
    File.WriteAllBytes(achievementPath, achievementFixture);
    Check(window.LoadSave(achievementPath), "Achievement fixture did not load.");
    window.ShowAchievements();
    VerifyPageSpacing();
    var achievementList = window.FindControl<ListBox>("AchievementList")!;
    var categoryFilter = window.FindControl<ComboBox>("AchievementCategoryFilter")!;
    var statusFilter = window.FindControl<ComboBox>("AchievementStatusFilter")!;
    var allAchievements = window.FindControl<Button>("UnlockAllAchievementsButton")!;
    var singleAchievement = window.FindControl<Button>("UnlockAchievementButton")!;
    var repairAchievement = window.FindControl<Button>("RepairAchievementCounterButton")!;
    var repairAllAchievements = window.FindControl<Button>("RepairAllAchievementCountersButton")!;
    Check(achievementList.ItemCount == 200 && allAchievements.IsEnabled && singleAchievement.IsEnabled,
        "Achievement page did not expose all supported entries.");
    Check(!repairAchievement.IsVisible && !repairAllAchievements.IsEnabled,
        "Incomplete achievements expose counter repair instead of unlocking.");
    categoryFilter.SelectedIndex = 2;
    Check(achievementList.ItemCount == 50, "Record category filter differs.");
    statusFilter.SelectedIndex = 1;
    int selectedAchievement = (int)achievementList.SelectedItem!.GetType().GetProperty("Id")!.GetValue(achievementList.SelectedItem)!;
    singleAchievement.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session!.Document.GetAchievement(selectedAchievement).Completed && achievementList.ItemCount == 49,
        "Single unlock did not update the incomplete filter.");
    window.FindControl<TextBox>("AchievementSearch")!.Text = "unlikely-to-match-any-achievement";
    Dispatcher.UIThread.RunJobs();
    Check(achievementList.ItemCount == 0 && !window.FindControl<StackPanel>("AchievementDetails")!.IsVisible,
        "Empty achievement search retained stale details.");
    allAchievements.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.Achievements.All(item => item.Completed) && !allAchievements.IsEnabled,
        "Bulk unlock only processed the filtered list.");
    Check(window.FindControl<TextBlock>("AchievementCountValue")!.Text == "200/200", "Achievement count is not global.");
    categoryFilter.SelectedIndex = 0;
    statusFilter.SelectedIndex = 2;
    window.FindControl<TextBox>("AchievementSearch")!.Text = "";
    Dispatcher.UIThread.RunJobs();
    Check(achievementList.ItemCount == 200 && !singleAchievement.IsEnabled, "Completed achievement state differs.");
    foreach (var language in UiLanguage.Languages)
    {
        window.SetLanguage(language.Key);
        Check(singleAchievement.Content?.ToString() == UiLanguage.Get("UnlockAchievement")
            && categoryFilter.SelectedIndex == 0 && statusFilter.SelectedIndex == 2,
            "Achievement language switch changed filters or retained stale UI text.");
    }
    window.SetLanguage("en");
    window.Width = 860;
    window.Height = 600;
    VerifyPageSpacing();
    Check(Math.Abs(statusFilter.Bounds.Top - categoryFilter.Bounds.Top) < 0.1
        && statusFilter.Bounds.Left - categoryFilter.Bounds.Right >= 11.9, "Achievement filters lack horizontal separation.");
    window.Width = 1120;
    window.Height = 780;
    byte[] achievementEdited = window.Session.Document.Serialize();
    Check(window.SaveTo(Path.Combine(temporary, "achievements-edited.sav")), "Achievement GUI changes could not save.");
    Check(File.ReadAllBytes(achievementPath).AsSpan().SequenceEqual(achievementFixture), "Achievement GUI changed the source.");
    for (int offset = 0; offset < achievementEdited.Length; offset++)
        if (offset is not (>= 0x557 and < 0x570) and not (>= 0xe30 and < 0xfc0))
            Check(achievementEdited[offset] == achievementFixture[offset], "Achievement GUI changed unrelated data.");
    byte[] inconsistentFixture = (byte[])achievementFixture.Clone();
    inconsistentFixture[0x557] = 128;
    BinaryPrimitives.WriteUInt16LittleEndian(inconsistentFixture.AsSpan(0xe3e), 4256);
    string inconsistentPath = Path.Combine(temporary, "unmet-counter.sav");
    File.WriteAllBytes(inconsistentPath, inconsistentFixture);
    Check(window.LoadSave(inconsistentPath), "Below-threshold completed fixture did not load.");
    statusFilter.SelectedIndex = 3;
    Check(achievementList.ItemCount == 1 && !singleAchievement.IsEnabled,
        "Unmet completed counter filter differs or permits redundant unlock.");
    Check(!singleAchievement.IsVisible && repairAchievement.IsVisible && repairAchievement.IsEnabled
        && repairAllAchievements.IsEnabled, "Unmet completed counter lacks single or bulk repair.");
    foreach (var language in UiLanguage.Languages)
    {
        window.SetLanguage(language.Key);
        Check(window.FindControl<TextBlock>("AchievementStatusValue")!.Text == UiLanguage.Get("AchievementUnmetCounter")
            && window.FindControl<TextBlock>("AchievementProgressValue")!.Text == "4,256 / 5,000"
            && statusFilter.SelectedIndex == 3, "An unmet completed counter was hidden by language switching.");
        Check(repairAchievement.Content?.ToString() == UiLanguage.Get("RepairAchievementCounter")
            && repairAllAchievements.Content?.ToString() == UiLanguage.Get("RepairAllAchievementCounters"),
            "Achievement counter repair retained stale language labels.");
        window.Width = 860;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        var label = repairAllAchievements.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == UiLanguage.Get("RepairAllAchievementCounters"));
        Check(label.Bounds.Width <= repairAllAchievements.Bounds.Width && label.Bounds.Height <= repairAllAchievements.Bounds.Height,
            "Bulk counter repair clips its translated label at minimum width.");
    }
    window.Width = 1120;
    window.Height = 780;
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(inconsistentFixture),
        "Inspecting inconsistent achievement state normalized the save.");
    repairAchievement.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    byte[] repairedFixture = (byte[])inconsistentFixture.Clone();
    BinaryPrimitives.WriteUInt16LittleEndian(repairedFixture.AsSpan(0xe3e), 5000);
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(repairedFixture)
        && achievementList.ItemCount == 0 && !repairAllAchievements.IsEnabled && !repairAchievement.IsEnabled
        && window.Session.HasChanges, "Single counter repair changed unrelated data or retained stale filtered rows.");
    Check(File.ReadAllBytes(inconsistentPath).AsSpan().SequenceEqual(inconsistentFixture), "Counter repair overwrote its source before Save.");
    Check(window.SaveTo(Path.Combine(temporary, "repaired-counter.sav")), "Repaired counter could not save.");
    byte[] mixedCounters = (byte[])inconsistentFixture.Clone();
    int dayBit = 0x2838 + 129;
    mixedCounters[0x50 + (dayBit >> 3)] |= (byte)(1 << (dayBit & 7));
    BinaryPrimitives.WriteUInt16LittleEndian(mixedCounters.AsSpan(0xe30 + 129 * 2), 320);
    File.WriteAllBytes(inconsistentPath, mixedCounters);
    Check(window.LoadSave(inconsistentPath), "Mixed completed counters did not load.");
    categoryFilter.SelectedIndex = 2;
    window.FindControl<TextBox>("AchievementSearch")!.Text = "unlikely-to-match-any-achievement";
    Check(achievementList.ItemCount == 0 && repairAllAchievements.IsEnabled, "Empty filters disable global counter repair.");
    repairAllAchievements.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    byte[] mixedExpected = (byte[])mixedCounters.Clone();
    BinaryPrimitives.WriteUInt16LittleEndian(mixedExpected.AsSpan(0xe3e), 5000);
    BinaryPrimitives.WriteUInt16LittleEndian(mixedExpected.AsSpan(0xe30 + 129 * 2), 366);
    Check(window.Session.Document.Serialize().AsSpan().SequenceEqual(mixedExpected) && !repairAllAchievements.IsEnabled
        && window.FindControl<TextBlock>("AchievementCountValue")!.Text == "2/200" && allAchievements.IsEnabled,
        "Bulk repair followed filters, unlocked other achievements or changed unrelated fields.");
    Check(File.ReadAllBytes(inconsistentPath).AsSpan().SequenceEqual(mixedCounters), "Bulk counter repair overwrote its source before Save.");
    Check(window.SaveTo(Path.Combine(temporary, "repaired-all-counters.sav")), "Bulk counter repair could not save.");
    window.SetLanguage("en");
    categoryFilter.SelectedIndex = 0;
    statusFilter.SelectedIndex = 0;
    window.FindControl<TextBox>("AchievementSearch")!.Text = "";
    BinaryPrimitives.WriteUInt16LittleEndian(achievementFixture.AsSpan(0x15231a), 14);
    BinaryPrimitives.WriteUInt32LittleEndian(achievementFixture.AsSpan(0x152368 + 13 * 0x138), 20);
    File.WriteAllBytes(achievementPath, achievementFixture);
    Check(window.LoadSave(achievementPath) && achievementList.ItemCount == 0 && !allAchievements.IsEnabled
        && !repairAllAchievements.IsEnabled && !repairAchievement.IsEnabled
        && window.FindControl<TextBlock>("AchievementsUnavailableValue")!.IsVisible,
        "Future Connected incorrectly exposes missing achievements.");
    if (args is ["--screenshot", var realSave, var screenshot, .. var page])
    {
        // Use a fresh renderer: resizing the headless test window retains stale clipping masks.
        window.Hide();
        window = new MainWindow();
        if (page.Length > 0 && page[0] == "regions") window.Height = 960;
        window.Show();
        bool lockedSkillsPreview = page.Length > 0 && page[0] == "skills-locked";
        Check(window.LoadSave(lockedSkillsPreview ? skillsPath : realSave), "Could not open the screenshot save.");
        if (page.Length > 0 && page[0] == "inventory")
            window.ShowInventory(page.Length > 2 ? Enum.Parse<InventoryKind>(page[2]) : InventoryKind.Collectables);
        else if (page.Length > 0 && page[0] == "equipment-inventory")
            window.ShowInventoryEquipment(page.Length > 2 ? Enum.Parse<EquipmentSlot>(page[2]) : EquipmentSlot.Weapon);
        else if (page.Length > 0 && page[0] == "gems")
        {
            window.ShowGems();
            if (page.Length > 2) window.FindControl<ListBox>("GemList")!.SelectedIndex = int.Parse(page[2]);
        }
        else if (page.Length > 0 && page[0] == "arts")
        {
            window.ShowArts();
            window.FindControl<ListBox>("ArtList")!.SelectedIndex = 2;
        }
        else if (page.Length > 0 && page[0] is "skills" or "skills-locked")
        {
            window.ShowSkills();
            Check(window.FindControl<Button>("MaxAllSkillsButton")!.IsEnabled
                && window.FindControl<Button>("MaxCharacterSkillsButton")!.IsEnabled,
                "Real main-story save cannot maximize skills.");
        }
        else if (page.Length > 0 && page[0] == "skill-links") window.ShowSkillLinks();
        else if (page.Length > 0 && page[0] == "affinity") window.ShowAffinity();
        else if (page.Length > 0 && page[0] == "regions") window.ShowRegionAffinity();
        else if (page.Length > 0 && page[0] == "collectopaedia") window.ShowCollectopaedia();
        else if (page.Length > 0 && page[0] == "equipment")
        {
            window.ShowEquipment();
            if (page.Length > 2) window.FindControl<ListBox>("CharacterList")!.SelectedIndex = int.Parse(page[2]);
        }
        else if (page.Length > 0 && page[0] == "achievements")
        {
            window.ShowAchievements();
            if (page.Length > 2) window.FindControl<ListBox>("AchievementList")!.SelectedIndex = int.Parse(page[2]);
        }
        else if (page.Length > 0 && page[0] == "characters")
        {
            window.ShowCharacters();
            window.FindControl<TabStrip>("CharacterNavigation")!.SelectedIndex = 0;
        }
        else window.FindControl<TabStrip>("MainNavigation")!.SelectedIndex = 0;
        if (page.Length > 1) window.SetLanguage(page[1]);
        Dispatcher.UIThread.RunJobs();
        VerifyPageSpacing();
        if (lockedSkillsPreview)
        {
            var scroll = window.FindControl<ScrollViewer>("SkillsScroll")!;
            var unlockButton = SkillControl<Button>("UnlockTreeButton", 4);
            Point point = unlockButton.TranslatePoint(new Point(0, 0), scroll)!.Value;
            scroll.Offset = new Vector(0, Math.Max(0, point.Y - scroll.Viewport.Height + unlockButton.Bounds.Height + 12));
            Dispatcher.UIThread.RunJobs();
        }
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
        Thread.Sleep(500); // Let card entrance and enabled-state transitions finish after the initial frame.
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(screenshot, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Console.WriteLine($"Screenshot: {screenshot}");
    }
}
finally { window.Close(); Directory.Delete(temporary, recursive: true); }
Console.WriteLine("GUI smoke tests passed.");
