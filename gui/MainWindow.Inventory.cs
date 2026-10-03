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
        if (GemsPanel is null || StackItemsPanel is null || _refreshingInventory) return;
        if (!CommitInventoryDraft() || !CommitGemDraft())
        {
            _refreshingInventory = true;
            ItemNavigation.SelectedIndex = _itemTab;
            _refreshingInventory = false;
            return;
        }
        _itemTab = ItemNavigation.SelectedIndex;
        GemsPanel.IsVisible = _itemTab == 0;
        StackItemsPanel.IsVisible = _itemTab != 0;
        _inventoryKind = _itemTab switch
        {
            2 => InventoryKind.Materials, 3 => InventoryKind.ArtManuals,
            4 => InventoryKind.KeyItems, _ => InventoryKind.Collectables
        };
        _inventoryItem = null;
        RefreshInventory();
        RefreshGems();
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
            AddInventoryButton.IsEnabled = writable;
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

    private async void AddInventory_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is null || _inventoryKind == InventoryKind.KeyItems || !CommitInventoryDraft()) return;
        var session = Session;
        var kind = _inventoryKind;
        var search = new TextBox { Name = "NewItemSearch", PlaceholderText = UiLanguage.Get("Search") };
        var item = new ComboBox { Name = "NewItemDefinition", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            DisplayMemberBinding = new Avalonia.Data.Binding("Label") };
        var quantity = new NumericUpDown { Name = "NewItemQuantity", Minimum = 1, Maximum = 99, Value = 1, FormatString = "0" };
        var error = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var add = new Button { Name = "CreateItem", Content = UiLanguage.Get("Add"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var cancel = new Button { Name = "CancelCreateItem", Content = UiLanguage.Get("Cancel"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var dialog = new Window
        {
            Title = UiLanguage.Get("AddItem"), Width = 440, SizeToContent = SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 12, Children =
            {
                search, item, new TextBlock { Text = UiLanguage.Get("Quantity") }, quantity, error, add, cancel
            } }
        };
        void RefreshChoices()
        {
            int? selected = (item.SelectedItem as InventoryChoice)?.Id;
            var choices = InventoryChoices(kind, search.Text?.Trim() ?? "");
            item.ItemsSource = choices;
            item.SelectedItem = choices.FirstOrDefault(choice => choice.Id == selected) ?? choices.FirstOrDefault();
            add.IsEnabled = choices.Length > 0;
        }
        search.TextChanged += (_, _) => RefreshChoices();
        RefreshChoices();
        add.Click += (_, _) =>
        {
            if (Session != session || item.SelectedItem is not InventoryChoice choice
                || !WholeNumber(quantity.Value, out uint count) || count is < 1 or > 99)
            { error.Text = UiLanguage.Get("InvalidValue"); return; }
            try
            {
                _inventorySelections[kind] = session.Document.AddInventoryItem(choice.Id, (int)count).Index;
                _refreshingInventory = true;
                InventorySearch.Text = "";
                _refreshingInventory = false;
                dialog.Close();
                RefreshInventory();
                ShowStatus(null);
            }
            catch (ArgumentException) { error.Text = UiLanguage.Get("InvalidInventory"); }
        };
        cancel.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    private void ApplyInventoryEdit(Action edit)
    {
        try { edit(); RefreshInventory(); ShowStatus(null); }
        catch (ArgumentException) { ShowStatus(UiLanguage.Get("InvalidInventory")); }
    }
}
