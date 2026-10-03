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
    var page = main.IsVisible ? (Control)main : characters;
    double gap = page.Bounds.Top - header.Bounds.Bottom;
    Check(Math.Abs(gap - (main.IsVisible ? 20 : 16)) < 0.1,
        $"Header-to-page spacing changed: {gap}.");
    if (!characters.IsVisible) return;
    var tabs = window.FindControl<TabStrip>("CharacterNavigation")!;
    Control body = tabs.SelectedIndex switch
    {
        1 => window.FindControl<Grid>("ArtsPanel")!,
        2 => window.FindControl<Grid>("SkillsPanel")!,
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
    File.WriteAllBytes(source, original);
    Check(window.LoadSave(source), "Could not load a game save.");
    Check(!window.Session!.HasChanges, "GUI load changes a save.");
    var money = window.FindControl<NumericUpDown>("MoneyInput")!;
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
    artSearch.Text = "no matching art";
    Dispatcher.UIThread.RunJobs();
    maxArts.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(window.Session.Document.GetCharacter(1).GetArt(11).Level == 12, "Bulk art maximum is disconnected.");
    artSearch.Text = "";
    Dispatcher.UIThread.RunJobs();
    Check(!window.Session.Document.GetCharacter(1).GetArt(17).Learned, "GUI bulk maximum unlocked an unlearned art.");
    artList.SelectedIndex = 0;
    Check(!artLevel.IsEnabled && !maxArt.IsEnabled, "Fixed talent art is editable.");
    Check(!artLevel.IsVisible && window.FindControl<TextBlock>("ArtLevelValue")!.IsVisible,
        "Readonly talent level is shown as an input.");
    foreach (string language in UiLanguage.Languages.Keys)
    {
        window.SetLanguage("zh-Hans");
        window.SetLanguage(language);
        Dispatcher.UIThread.RunJobs();
        Check(maxArts.Content?.ToString() == UiLanguage.Get("MaxCharacterArts"), "Art button translation is stale.");
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
    Check(window.SaveTo(Path.Combine(temporary, "arts-edited.sav")), "Could not save GUI art edits.");
    var artsSaved = SaveDocument.Parse(File.ReadAllBytes(Path.Combine(temporary, "arts-edited.sav")));
    Check(artsSaved.GetCharacter(1).GetArt(12).Level == 12, "GUI art edit was not persisted.");
    Check(File.ReadAllBytes(source).AsSpan().SequenceEqual(beforeArtEditing), "GUI art edit overwrote the input save.");
    byte[] skillsFixture = (byte[])original.Clone();
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
    Check(window.Session.Document.Characters.All(character => character.GetSkillTree(4).LearnedCount == 0
        && character.GetSkillTree(5).LearnedCount == 0), "Skill batch actions unlocked blocked trees.");
    search.Text = "";
    foreach (string language in UiLanguage.Languages.Keys)
    {
        window.SetLanguage(language);
        VerifyPageSpacing();
        Check(maxAllSkills.Content?.ToString() == UiLanguage.Get("MaxAllSkills")
            && window.FindControl<Button>("MaxCharacterSkillsButton")!.Content?.ToString() == UiLanguage.Get("MaxCharacterSkills"),
            "Skill batch button translation is stale.");
        Check(SkillControl<Button>("MaxTreeButton").Content?.ToString() == UiLanguage.Get("MaxTree"),
            "Single-branch button translation is stale.");
    }
    window.Width = 860;
    window.Height = 600;
    VerifyPageSpacing();
    window.Width = 1120;
    window.Height = 780;
    window.ShowArts();
    VerifyPageSpacing();
    Check(!maxAllSkills.IsVisible, "Max All Skills remains visible outside Skills.");
    Check(File.ReadAllBytes(skillsPath).AsSpan().SequenceEqual(skillsFixture), "GUI skill editing overwrote its source.");
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
    if (args is ["--screenshot", var realSave, var screenshot, .. var page])
    {
        Check(window.LoadSave(realSave), "Could not open the screenshot save.");
        if (page.Length > 0 && page[0] == "arts")
        {
            window.ShowArts();
            window.FindControl<ListBox>("ArtList")!.SelectedIndex = 2;
        }
        else if (page.Length > 0 && page[0] == "skills")
        {
            window.ShowSkills();
            Check(window.FindControl<Button>("MaxAllSkillsButton")!.IsEnabled
                && window.FindControl<Button>("MaxCharacterSkillsButton")!.IsEnabled,
                "Real main-story save cannot maximize skills.");
        }
        else if (page.Length > 0 && page[0] == "characters")
        {
            window.ShowCharacters();
            window.FindControl<TabStrip>("CharacterNavigation")!.SelectedIndex = 0;
        }
        else window.FindControl<TabStrip>("MainNavigation")!.SelectedIndex = 0;
        if (page.Length > 1) window.SetLanguage(page[1]);
        Thread.Sleep(250); // Allow enabled-state color transitions to finish before capture.
        Dispatcher.UIThread.RunJobs();
        VerifyPageSpacing();
        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(screenshot, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Console.WriteLine($"Screenshot: {screenshot}");
    }
}
finally { window.Close(); Directory.Delete(temporary, recursive: true); }
Console.WriteLine("GUI smoke tests passed.");
