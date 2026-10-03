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

internal static class Colony6GuiTests
{
    internal static void Run(MainWindow window, string temporary, Action<bool, string> check)
    {
        var button = window.FindControl<Button>("MaxColony6Button")!;
        var card = window.FindControl<Control>("Colony6Card")!;
        byte[] bytes = new byte[SaveDocument.FileSize];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x152318), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x15231a), 2);
        bytes[0x152330] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x152368), 20);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x1524a0), 20);
        bytes = Colony6Tests.StartedFixture(bytes);
        string path = Path.Combine(temporary, "colony6.sav");
        string output = Path.Combine(temporary, "colony6-edited.sav");
        File.WriteAllBytes(path, bytes);
        check(window.LoadSave(path), "Colony GUI fixture did not load.");
        window.FindControl<TabStrip>("MainNavigation")!.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        check(card.IsVisible && button.IsEnabled
            && window.FindControl<TextBlock>("Colony6HousingValue")!.Text == "1"
            && window.FindControl<TextBlock>("Colony6NatureValue")!.Text == "0",
            "Started colony does not show its actual facility levels.");
        check(!card.GetVisualDescendants().Any(control => control is TextBox or NumericUpDown or ScrollViewer),
            "Colony state is presented as editable inputs or has an internal scroll area.");
        window.Width = 860;
        window.Height = 600;
        foreach (string language in UiLanguage.Languages.Keys)
        {
            window.SetLanguage(language);
            Dispatcher.UIThread.RunJobs();
            check(button.Content?.ToString() == UiLanguage.Get("MaxColony6"), "Colony button has a stale translation.");
            var position = button.TranslatePoint(new Point(0, 0), card)!.Value;
            check(position.X >= 20 && position.X + button.Bounds.Width <= card.Bounds.Width - 19.9
                && position.Y + button.Bounds.Height <= card.Bounds.Height - 19.9,
                "Colony card does not grow naturally around its action button.");
            check(window.Session!.Document.Serialize().AsSpan().SequenceEqual(bytes), "Colony language change mutated the save.");
        }
        window.SetLanguage("en");
        window.Width = 1120;
        window.Height = 780;
        window.ShowCharacters();
        var level = window.FindControl<ComboBox>("LevelInput")!;
        level.SelectedItem = 21u;
        window.FindControl<TabStrip>("MainNavigation")!.SelectedIndex = 0;
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        byte[] expected = Colony6Tests.ExpectedMaximum(bytes);
        check(window.Session!.Document.Serialize().AsSpan().SequenceEqual(expected)
            && window.Session.Document.Colony6 is { IsMaximum: true } && !button.IsEnabled,
            "Colony GUI action misses linked state or does not disable after reaching maximum.");
        check(level.SelectedItem is 21u && window.Session.Document.GetCharacter(1).Level == 20,
            "Colony refresh discarded or silently applied another panel's progression draft.");
        level.SelectedItem = 20u;
        check(window.SaveTo(output) && File.ReadAllBytes(output).AsSpan().SequenceEqual(expected),
            "Colony GUI action was not saved exactly.");
        check(File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes), "Colony GUI test changed its source save.");
        foreach (bool future in new[] { false, true })
        {
            byte[] protectedBytes = (byte[])bytes.Clone();
            if (future)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(protectedBytes.AsSpan(0x15231a), 14);
                BinaryPrimitives.WriteUInt32LittleEndian(protectedBytes.AsSpan(0x152368 + 13 * 0x138), 20);
            }
            else BinaryPrimitives.WriteUInt32LittleEndian(protectedBytes, 8);
            File.WriteAllBytes(output, protectedBytes);
            check(window.LoadSave(output) && !button.IsEnabled && card.IsVisible == !future,
                "Unverified campaign or format exposes reconstruction mutation.");
        }
        bytes.AsSpan(0xcfa, 5).Clear();
        File.WriteAllBytes(output, bytes);
        check(window.LoadSave(output) && !button.IsEnabled && ToolTip.GetTip(button) is string,
            "Unstarted reconstruction is not protected or has no reason available.");
    }
}
