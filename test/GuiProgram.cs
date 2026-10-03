using System.Buffers.Binary;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
Check(!window.FindControl<StackPanel>("ResourceInputs")!.IsEnabled, "Empty form is editable.");
Check(window.FindControl<ScrollViewer>("MainScroll") is not null, "Main page has no shared scroll container.");
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
    Check(window.FindControl<TextBlock>("LevelValue")!.Text == "20", "Character level mapping differs.");
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
        Check(window.FindControl<MenuItem>("SaveMenu")!.Header?.ToString() == UiLanguage.Get("Save"), "Menu translation is stale.");
        Check(window.FindControl<TextBlock>("CampaignValue")!.Text == UiLanguage.Get("MainStory"), "Campaign translation is stale.");
        Check(window.Session.Document.Money == 123, "Language switch changed edits.");
        Check(window.Session.Document.GetCharacter(1).AP == 321, "Language switch changed character edits.");
        Check(window.Session.Document.GetCharacter(1).ReserveExperience == 10000, "Language switch changed reserve EXP.");
        Check(window.FindControl<Button>("MaxAllAPButton")!.Content?.ToString() == UiLanguage.Get("MaxAllAP"),
            "Bulk AP button translation is stale.");
        Check(window.FindControl<Button>("MaxReserveExperienceButton")!.Content?.ToString() == UiLanguage.Get("MaxReserveExperience"),
            "Reserve EXP button translation is stale.");
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
    string output = Path.Combine(temporary, "edited.sav");
    Check(window.SaveTo(output), "GUI save failed.");
    Check(SaveDocument.Parse(File.ReadAllBytes(output)).Money == 123, "Saved amount differs.");
    Check(File.ReadAllBytes(source).AsSpan().SequenceEqual(original), "GUI test modified its input.");
    Check(!window.LoadSave(Path.Combine(temporary, "missing.sav")), "Missing save was accepted.");
    Check(window.Session.Document.Money == 123, "Failed open replaced the current document.");
    Check(window.SaveTo(source), "Could not save the synthetic source before closing.");
    byte[] ambiguous = (byte[])original.Clone();
    ambiguous[0x152330] = 1;
    string ambiguousPath = Path.Combine(temporary, "ambiguous.sav");
    File.WriteAllBytes(ambiguousPath, ambiguous);
    Check(window.LoadSave(ambiguousPath), "Could not open an ambiguous campaign.");
    Check(window.FindControl<TextBlock>("CampaignValue")!.Text == UiLanguage.Get("Unknown"),
        "An ambiguous campaign was shown as the main story.");
    Check(!window.FindControl<StackPanel>("AffinityCoinsField")!.IsVisible,
        "An ambiguous campaign exposes coin editing.");
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
        if (page is ["characters"]) window.ShowCharacters();
        else window.FindControl<TabStrip>("MainNavigation")!.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(screenshot, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Console.WriteLine($"Screenshot: {screenshot}");
    }
}
finally { window.Close(); Directory.Delete(temporary, recursive: true); }
Console.WriteLine("GUI smoke tests passed.");
