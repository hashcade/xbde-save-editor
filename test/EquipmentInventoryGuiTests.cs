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

internal static class EquipmentInventoryGuiTests
{
    internal static void Run(MainWindow window, string temporary, Action<bool, string> check)
    {
        byte[] bytes = EquipmentInventoryTests.Fixture(new byte[SaveDocument.FileSize]);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x152318), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x15231a), 2);
        bytes[0x152330] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x152368), 20);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x1524a0), 20);
        string source = Path.Combine(temporary, "equipment-inventory.sav");
        string output = Path.Combine(temporary, "equipment-inventory-output.sav");
        File.WriteAllBytes(source, bytes);
        byte[] sourceBytes = (byte[])bytes.Clone();
        check(window.LoadSave(source), "Equipment inventory fixture did not load.");
        window.ShowInventoryEquipment(EquipmentSlot.Weapon);
        var list = window.FindControl<ListBox>("EquipmentInventoryList")!;
        var recipient = window.FindControl<ComboBox>("EquipmentRecipientInput")!;
        var definitions = window.FindControl<ComboBox>("EquipmentDefinitionInput")!;
        var add = window.FindControl<Button>("CreateEquipmentButton")!;
        var delete = window.FindControl<Button>("DeleteEquipmentButton")!;
        var fill = window.FindControl<Button>("FillEquipmentButton")!;
        var favorite = window.FindControl<CheckBox>("EquipmentFavoriteInput")!;
        var creationCard = window.FindControl<Control>("CreateEquipmentCard")!;
        var ownedCard = window.FindControl<Control>("OwnedEquipmentCard")!;
        check(list.ItemCount == 0 && !ownedCard.IsVisible && creationCard.IsVisible,
            "Empty inventory blocks independent equipment creation.");
        check(window.SaveTo(output) && File.ReadAllBytes(output).AsSpan().SequenceEqual(bytes),
            "Selecting an equipment creation definition silently creates equipment on save.");
        recipient.SelectedIndex = 1;
        var expected = SaveDocument.Parse(bytes);
        var definition = expected.CreatableEquipment(EquipmentSlot.Weapon).First(item => item.Characters.Contains(2));
        expected.AddEquipment(definition.Id);
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.Session!.Document.Serialize().AsSpan().SequenceEqual(expected.Serialize()) && list.ItemCount == 1 && ownedCard.IsVisible,
            "GUI creation did not use the native initializer or select its new record.");
        favorite.IsChecked = true;
        expected.GetInventoryEquipment(EquipmentSlot.Weapon, 0).SetFavorite(true);
        check(window.Session.Document.Serialize().AsSpan().SequenceEqual(expected.Serialize()), "GUI favorite changed the wrong equipment.");
        definitions.SelectedIndex = -1;
        check(!add.IsEnabled && delete.IsEnabled, "Creation choice and selected-item operations are coupled.");
        delete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        expected.GetInventoryEquipment(EquipmentSlot.Weapon, 0).Delete();
        check(window.Session.Document.Serialize().AsSpan().SequenceEqual(expected.Serialize()) && list.ItemCount == 0,
            "Equipment deletion shifts inventory or changes its gems.");
        window.FindControl<TextBox>("EquipmentInventorySearch")!.Text = "no visible equipment";
        fill.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        expected.FillMissingEquipment(EquipmentSlot.Weapon, 2);
        check(window.Session.Document.Serialize().AsSpan().SequenceEqual(expected.Serialize()) && list.ItemCount == 0 && !fill.IsEnabled,
            "Bulk equipment filling is affected by the inventory search or misses definitions.");
        window.FindControl<TextBox>("EquipmentInventorySearch")!.Text = "";
        Dispatcher.UIThread.RunJobs();
        check(list.ItemCount == expected.InventoryEquipment(EquipmentSlot.Weapon).Count, "Bulk-created equipment is missing from the list.");
        check(window.SaveTo(output) && File.ReadAllBytes(output).AsSpan().SequenceEqual(expected.Serialize()), "GUI equipment edits were not saved exactly.");
        window.Width = 860;
        window.Height = 600;
        foreach (string language in UiLanguage.Languages.Keys)
        {
            window.SetLanguage(language);
            Dispatcher.UIThread.RunJobs();
            check(fill.Content?.ToString() == UiLanguage.Get("FillMissingEquipment") && add.Content?.ToString() == UiLanguage.Get("Add"),
                "Equipment actions have stale translations.");
            check(window.Session.Document.Serialize().AsSpan().SequenceEqual(expected.Serialize()), "Equipment language switch changed bytes.");
            var panel = window.FindControl<Grid>("EquipmentInventoryPanel")!;
            var tabs = window.FindControl<TabStrip>("ItemNavigation")!;
            check(Math.Abs(panel.Bounds.Top - tabs.Bounds.Bottom - 16) < 0.1, "Equipment tabs have duplicate spacing.");
            var deletePosition = delete.TranslatePoint(new Point(0, 0), panel)!.Value;
            check(deletePosition.X >= 20 && deletePosition.X + delete.Bounds.Width <= 260.1
                && deletePosition.Y + delete.Bounds.Height <= panel.Bounds.Height - 19.9,
                "Equipment list actions escaped or clipped their left card.");
            var createPosition = add.TranslatePoint(new Point(0, 0), creationCard)!.Value;
            check(createPosition.X >= 20 && createPosition.X + add.Bounds.Width <= creationCard.Bounds.Width - 19.9,
                "Equipment creation controls are clipped horizontally.");
            check(list.GetVisualDescendants().OfType<VirtualizingStackPanel>().Any(panel => panel.CacheLength == 1),
                "Equipment inventory lost its virtual-list rendering buffer.");
        }
        window.SetLanguage("en");
        window.Width = 1120;
        window.Height = 780;
        foreach (var slot in Enum.GetValues<EquipmentSlot>().Skip(1))
        {
            window.ShowInventoryEquipment(slot);
            check(window.FindControl<TextBlock>("EquipmentInventoryTitle")!.Text == UiLanguage.Get(slot.ToString())
                && definitions.ItemCount > 0 && add.IsEnabled, "Armor bank has no compatible creation definitions.");
            var armor = expected.CreatableEquipment(slot).First(item => item.Characters.Contains(2));
            expected.AddEquipment(armor.Id);
            add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(window.Session.Document.Serialize().AsSpan().SequenceEqual(expected.Serialize()), "Armor creation used the wrong inventory bank.");
        }
        window.ShowCharacters();
        var level = window.FindControl<ComboBox>("LevelInput")!;
        level.SelectedItem = 21u;
        window.ShowInventoryEquipment(EquipmentSlot.Head);
        favorite.IsChecked = true;
        expected.GetInventoryEquipment(EquipmentSlot.Head, 0).SetFavorite(true);
        check(level.SelectedItem is 21u && window.Session.Document.GetCharacter(1).Level == 20
            && window.Session.Document.Serialize().AsSpan().SequenceEqual(expected.Serialize()),
            "Equipment inventory refresh discarded or silently applied a progression draft.");
        byte[] equipped = expected.Serialize();
        BinaryPrimitives.WriteUInt32LittleEndian(equipped.AsSpan(0x1524a0 + 0x28), 2u << 16);
        File.WriteAllBytes(output, equipped);
        check(window.LoadSave(output), "Equipped inventory fixture did not load.");
        window.ShowInventoryEquipment(EquipmentSlot.Weapon);
        list.SelectedIndex = 0;
        check(!delete.IsEnabled && window.FindControl<TextBlock>("OwnedEquipmentUsers")!.Text == CharacterCatalog.Get(2, "en"),
            "Equipped equipment is deletable or has no owner label.");
        byte[] full = (byte[])bytes.Clone();
        for (int index = 0; index < 499; index++) full[0x3b10 + index * 0x30 + 0x10] = 2;
        File.WriteAllBytes(output, full);
        check(window.LoadSave(output), "Full inventory fixture did not load.");
        recipient.SelectedIndex = 1;
        fill.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.Session.Document.Serialize().AsSpan().SequenceEqual(full) && window.FindControl<TextBlock>("StatusMessage")!.IsVisible,
            "Full GUI inventory was partially filled or has no error.");
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 8);
        File.WriteAllBytes(output, bytes);
        check(window.LoadSave(output) && !add.IsEnabled && !fill.IsEnabled && !delete.IsEnabled && !favorite.IsEnabled,
            "Unverified save format exposes equipment mutation controls.");
        check(File.ReadAllBytes(source).AsSpan().SequenceEqual(sourceBytes), "Equipment GUI tests changed their source save.");
        window.ShowGems();
    }
}
