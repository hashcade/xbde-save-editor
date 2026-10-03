using Avalonia.Controls;
using Avalonia.Interactivity;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record InventoryRow(int Index, string Label);
    private sealed record InventoryChoice(int Id, string Label);
    private InventoryKind _inventoryKind = InventoryKind.Collectables;
    private InventoryItemRecord? _inventoryItem;
    private bool _refreshingInventory;
    private readonly Dictionary<InventoryKind, int?> _inventorySelections = [];
    private int _itemTab;
    private InventoryKind? _creationInventoryKind;

    private bool HasInventoryDraft => _inventoryItem?.CanEdit == true
        && (InventoryQuantityInput.Value != _inventoryItem.Quantity || InventoryFavoriteInput.IsChecked != _inventoryItem.Favorite);

    public void ShowInventory(InventoryKind kind)
    {
        ShowGems();
        ItemNavigation.SelectedIndex = kind switch
        {
            InventoryKind.Collectables => 1, InventoryKind.Materials => 2,
            InventoryKind.ArtManuals => 3, InventoryKind.KeyItems => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private void ItemNavigation_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (GemsPanel is null || StackItemsPanel is null || EquipmentInventoryPanel is null || _refreshingInventory) return;
        if (!CommitInventoryDraft() || !CommitGemDraft())
        {
            _refreshingInventory = true;
            ItemNavigation.SelectedIndex = _itemTab;
            _refreshingInventory = false;
            return;
        }
        _itemTab = ItemNavigation.SelectedIndex;
        GemsPanel.IsVisible = _itemTab == 0;
        StackItemsPanel.IsVisible = _itemTab is >= 1 and <= 4;
        EquipmentInventoryPanel.IsVisible = _itemTab is 5 or 6;
        if (_itemTab == 5) _equipmentInventorySlot = EquipmentSlot.Weapon;
        else if (_itemTab == 6 && _equipmentInventorySlot == EquipmentSlot.Weapon) _equipmentInventorySlot = EquipmentSlot.Head;
        _inventoryKind = _itemTab switch
        {
            2 => InventoryKind.Materials, 3 => InventoryKind.ArtManuals,
            4 => InventoryKind.KeyItems, _ => InventoryKind.Collectables
        };
        _inventoryItem = null;
        RefreshInventory();
        RefreshGems();
        RefreshEquipmentInventory();
    }

    private void RefreshInventory()
    {
        if (InventoryList is null) return;
        _refreshingInventory = true;
        try
        {
            string query = InventorySearch.Text?.Trim() ?? "";
            var items = Session?.Document.Inventory(_inventoryKind) ?? [];
            var rows = items.Select(item => new InventoryRow(item.Index, $"{item.Name} · ×{item.Quantity}"))
                .Where(row => row.Label.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            InventoryList.ItemsSource = rows;
            InventoryList.SelectedItem = rows.FirstOrDefault(row => row.Index == _inventorySelections.GetValueOrDefault(_inventoryKind)) ?? rows.FirstOrDefault();
            _inventorySelections[_inventoryKind] = (InventoryList.SelectedItem as InventoryRow)?.Index;
            _inventoryItem = _inventorySelections[_inventoryKind] is { } index ? Session?.Document.GetInventoryItem(_inventoryKind, index) : null;
            InventoryTitle.Text = UiLanguage.Get(_inventoryKind.ToString());
            InventoryCountValue.Text = $"{items.Count}/{InventoryCatalog.Capacity}";
            bool editable = _inventoryItem?.CanEdit == true;
            InventoryEditorCard.IsVisible = _inventoryItem is not null;
            InventoryEditor.IsVisible = _inventoryItem is not null && editable;
            InventoryEditor.IsEnabled = editable;
            InventoryReadOnly.IsVisible = _inventoryItem is not null && !editable;
            InventoryQuantityInput.Maximum = Math.Max(99, _inventoryItem?.Quantity ?? 0);
            InventoryQuantityInput.Value = _inventoryItem?.Quantity ?? 1;
            InventoryFavoriteInput.IsChecked = _inventoryItem?.Favorite ?? false;
            InventoryQuantityValue.Text = _inventoryItem?.Quantity.ToString();
            bool writable = Session is not null && Session.Document.Campaign != Campaign.Unknown && _inventoryKind != InventoryKind.KeyItems;
            InventoryActions.IsVisible = _inventoryKind != InventoryKind.KeyItems;
            MaxInventoryButton.IsEnabled = writable && items.Count > 0 && items.All(item => item.CanEdit);
            CreateItemCard.IsVisible = _inventoryKind != InventoryKind.KeyItems;
            NewItemInputs.IsEnabled = writable;
            if (_creationInventoryKind != _inventoryKind)
            {
                _creationInventoryKind = _inventoryKind;
                NewItemSearch.Text = "";
                NewItemDefinition.SelectedItem = null;
                NewItemQuantity.Value = 1;
            }
            RefreshNewItemChoices();
            DeleteInventoryButton.IsEnabled = editable;
        }
        finally { _refreshingInventory = false; }
    }

    private static InventoryChoice[] InventoryChoices(InventoryKind kind, string query)
    {
        var definitions = InventoryCatalog.Definitions.Where(item => item.Kind == kind
            && item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        var repeatedNames = definitions.GroupBy(item => item.Name).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet();
        return definitions.Select(item => new InventoryChoice(item.Id,
            repeatedNames.Contains(item.Name) ? $"{item.Name} ({item.Id})" : item.Name)).ToArray();
    }

    private bool CommitInventoryDraft()
    {
        if (!HasInventoryDraft) return true;
        if (!WholeNumber(InventoryQuantityInput.Value, out uint quantity) || quantity is < 1 or > 99)
        { ShowStatus(UiLanguage.Get("InvalidValue")); return false; }
        try
        {
            if (quantity != _inventoryItem!.Quantity) _inventoryItem.SetQuantity((int)quantity);
            _inventoryItem.SetFavorite(InventoryFavoriteInput.IsChecked == true);
            ShowStatus(null);
            return true;
        }
        catch (ArgumentException) { ShowStatus(UiLanguage.Get("InvalidInventory")); return false; }
    }

    private void InventoryList_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingInventory) return;
        if (!CommitInventoryDraft())
        {
            _refreshingInventory = true;
            InventoryList.SelectedItem = InventoryList.Items.OfType<InventoryRow>()
                .FirstOrDefault(row => row.Index == _inventorySelections.GetValueOrDefault(_inventoryKind));
            _refreshingInventory = false;
            return;
        }
        _inventorySelections[_inventoryKind] = (InventoryList.SelectedItem as InventoryRow)?.Index;
        RefreshInventory();
    }

    private void InventorySearch_Changed(object? sender, TextChangedEventArgs e)
    { if (!_refreshingInventory && CommitInventoryDraft()) RefreshInventory(); }
    private void ApplyInventory_Click(object? sender, RoutedEventArgs e)
    { if (CommitInventoryDraft()) RefreshInventory(); }

    private void MaxInventory_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is null || !CommitInventoryDraft()) return;
        ApplyInventoryEdit(() => Session.Document.MaxInventoryQuantities(_inventoryKind));
    }

    private void DeleteInventory_Click(object? sender, RoutedEventArgs e)
    {
        if (_inventoryItem is null) return;
        ApplyInventoryEdit(() => _inventoryItem.Delete());
    }

    private void RefreshNewItemChoices()
    {
        int? selected = (NewItemDefinition.SelectedItem as InventoryChoice)?.Id;
        var choices = InventoryChoices(_inventoryKind, NewItemSearch.Text?.Trim() ?? "");
        NewItemDefinition.ItemsSource = choices;
        NewItemDefinition.SelectedItem = choices.FirstOrDefault(choice => choice.Id == selected) ?? choices.FirstOrDefault();
        UpdateCreateItemButton();
    }

    private void UpdateCreateItemButton() => CreateItem.IsEnabled = Session is not null
        && Session.Document.Campaign != Campaign.Unknown && _inventoryKind != InventoryKind.KeyItems
        && NewItemDefinition.SelectedItem is InventoryChoice;

    private void NewItemSearch_Changed(object? sender, TextChangedEventArgs e)
    { if (!_refreshingInventory && NewItemDefinition is not null) RefreshNewItemChoices(); }

    private void NewItemDefinition_Changed(object? sender, SelectionChangedEventArgs e)
    { if (!_refreshingInventory && CreateItem is not null) UpdateCreateItemButton(); }

    private void CreateItem_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is null || _inventoryKind == InventoryKind.KeyItems) return;
        if (NewItemDefinition.SelectedItem is not InventoryChoice choice
            || !WholeNumber(NewItemQuantity.Value, out uint count) || count is < 1 or > 99)
        { ShowStatus(UiLanguage.Get("InvalidValue")); return; }
        if (!CommitInventoryDraft()) return;
        ApplyInventoryEdit(() =>
        {
            _inventorySelections[_inventoryKind] = Session.Document.AddInventoryItem(choice.Id, (int)count).Index;
            _refreshingInventory = true;
            try { InventorySearch.Text = ""; }
            finally { _refreshingInventory = false; }
        });
    }

    private void ApplyInventoryEdit(Action edit)
    {
        try { edit(); RefreshInventory(); ShowStatus(null); }
        catch (ArgumentException) { ShowStatus(UiLanguage.Get("InvalidInventory")); }
    }
}
