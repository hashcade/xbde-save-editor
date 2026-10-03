using System.Buffers.Binary;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XbdeEditor.Core;
using XbdeEditor.Gui;
using XbdeEditor.Gui.Localization;

internal static class CollectopaediaGuiTests
{
    internal static void Run(MainWindow window, string temporary, Action<bool, string> check)
    {
        byte[] bytes = new byte[SaveDocument.FileSize];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 7);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x152318), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x15231a), 2);
        bytes[0x152330] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x152368), 20);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x1524a0), 20);
        bytes = EquipmentInventoryTests.Fixture(bytes);
        string path = Path.Combine(temporary, "collection.sav");
        string output = Path.Combine(temporary, "collection-edited.sav");
        File.WriteAllBytes(path, bytes);
        check(window.LoadSave(path), "Collection GUI fixture did not load.");
        window.ShowCollectopaedia();
        Dispatcher.UIThread.RunJobs();
        var panel = window.FindControl<Grid>("CollectopaediaPanel")!;
        var page = window.FindControl<ComboBox>("CollectionPageFilter")!;
        var status = window.FindControl<ComboBox>("CollectionStatusFilter")!;
        var list = window.FindControl<ListBox>("CollectionList")!;
        var single = window.FindControl<Button>("RegisterCollectionEntryButton")!;
        var completePage = window.FindControl<Button>("CompleteCollectionPageButton")!;
        var completeAll = window.FindControl<Button>("CompleteCollectionButton")!;
        check(panel.IsVisible && page.ItemCount == 21 && list.ItemCount > 0 && single.IsEnabled
            && completePage.IsEnabled && completeAll.IsEnabled
            && window.FindControl<TextBlock>("CollectionCountValue")!.Text == "0/300",
            "Collection GUI counts, campaign pages or actions are missing.");
        check(list.ItemsPanel.Build() is VirtualizingStackPanel { CacheLength: 1 },
            "Collection list has no shared render buffer.");
        check(completePage.Parent == completeAll.Parent
            && completePage.GetVisualAncestors().Contains(list.Parent!), "Collection bulk actions are not beside the list.");
        check(!window.FindControl<StackPanel>("CollectionEntryDetails")!.GetVisualDescendants()
            .Any(control => control is TextBox or NumericUpDown), "Read-only registration status uses an input.");
        window.Width = 1120;
        window.Height = 780;
        foreach (string language in UiLanguage.Languages.Keys)
        {
            window.SetLanguage(language);
            Dispatcher.UIThread.RunJobs();
            check(completeAll.Content?.ToString() == UiLanguage.Get("CompleteCollection"), "Collection action has a stale UI translation.");
            var position = completeAll.TranslatePoint(new Point(0, 0), panel)!.Value;
            check(position.X >= 20 && position.X + completeAll.Bounds.Width <= 320 - 19.9,
                "Collection bulk action is clipped or moved into the selected-entry editor.");
            check(window.Session!.Document.Serialize().AsSpan().SequenceEqual(bytes), "Collection language change altered the save.");
        }
        window.SetLanguage("en");
        window.ShowCharacters();
        var level = window.FindControl<ComboBox>("LevelInput")!;
        level.SelectedItem = 21u;
        window.ShowCollectopaedia();
        single.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.Session!.Document.Collectopaedia.Count(entry => entry.IsRegistered) == 1
            && !single.IsEnabled && window.FindControl<TextBlock>("CollectionEntryStatusValue")!.Text == UiLanguage.Get("Registered"),
            "Single-entry registration does not update its status or repeats an award.");
        status.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        check(list.ItemCount == CollectopaediaCatalog.Pages.First().Entries.Count - 1 && single.IsEnabled,
            "Registration status filter does not remove completed entries.");
        var firstPage = CollectopaediaCatalog.Pages.First();
        completePage.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.Session.Document.Collectopaedia.Count(entry => entry.IsRegistered) == firstPage.Entries.Count
            && list.ItemCount == 0 && !completePage.IsEnabled && completeAll.IsEnabled,
            "Page completion follows filtered rows instead of the full page.");
        page.SelectedIndex = 1;
        window.FindControl<TextBox>("CollectionSearch")!.Text = "no match";
        completeAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.Session.Document.Collectopaedia.All(entry => entry.IsRegistered)
            && !completeAll.IsEnabled && !completePage.IsEnabled,
            "Campaign completion follows current filters or fails to update its buttons.");
        check(level.SelectedItem is 21u && window.Session.Document.GetCharacter(1).Level == 20,
            "Collection completion applies or discards another panel's character draft.");
        level.SelectedItem = 20u;
        var expected = SaveDocument.Parse(bytes);
        expected.CompleteCollectopaedia(expected.Collectopaedia.Select(entry => entry.Id));
        check(window.SaveTo(output) && File.ReadAllBytes(output).AsSpan().SequenceEqual(expected.Serialize())
            && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes), "Collection GUI saving changes its source or reward records.");
        window.FindControl<TextBox>("CollectionSearch")!.Text = "";
        status.SelectedIndex = 0;
        byte[] full = (byte[])bytes.Clone();
        for (int index = 0; index < 500; index++) full[0x2c380 + index * 0x2c + 0x10] = 2;
        File.WriteAllBytes(output, full);
        check(window.LoadSave(output), "Full collection inventory fixture did not load.");
        completeAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.Session.Document.Serialize().AsSpan().SequenceEqual(full)
            && window.FindControl<TextBlock>("StatusMessage")!.Text is { Length: > 0 },
            "Failed collection GUI batch wrote partial flags or hid its failure.");
        byte[] future = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(future.AsSpan(0x15231a), 14);
        BinaryPrimitives.WriteUInt32LittleEndian(future.AsSpan(0x152368 + 13 * 0x138), 20);
        File.WriteAllBytes(output, future);
        check(window.LoadSave(output) && page.ItemCount == 2
            && window.FindControl<TextBlock>("CollectionCountValue")!.Text == "0/28",
            "Future Connected displays main-story collection pages.");
        completeAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var futureExpected = SaveDocument.Parse(future);
        futureExpected.CompleteCollectopaedia(futureExpected.Collectopaedia.Select(entry => entry.Id));
        check(window.Session.Document.Serialize().AsSpan().SequenceEqual(futureExpected.Serialize())
            && !completeAll.IsEnabled, "Future Connected collection completion misses rewards or changes achievements.");
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 8);
        File.WriteAllBytes(output, bytes);
        check(window.LoadSave(output) && !single.IsEnabled && !completeAll.IsEnabled && page.ItemCount == 0
            && window.FindControl<TextBlock>("CollectionUnavailableValue")!.IsVisible,
            "Unverified collection format exposes mutation actions.");
    }
}
