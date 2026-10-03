using Avalonia.Controls;
using Avalonia.Interactivity;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record EquipmentBankChoice(EquipmentSlot Slot, string Label)
    {
        public override string ToString() => Label;
    }
    private sealed record EquipmentRecipientChoice(int Id, string Label)
    {
        public override string ToString() => Label;
    }
    private EquipmentSlot _equipmentInventorySlot = EquipmentSlot.Weapon;
    private readonly Dictionary<EquipmentSlot, int?> _equipmentInventorySelections = [];
    private int? _equipmentRecipient;
    private bool _refreshingEquipmentInventory;

    public void ShowInventoryEquipment(EquipmentSlot slot)
    {
        if (!Enum.IsDefined(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
        MainNavigation.SelectedIndex = 2;
        _equipmentInventorySlot = slot;
        ItemNavigation.SelectedIndex = slot == EquipmentSlot.Weapon ? 5 : 6;
        RefreshEquipmentInventory();
    }

    private void RefreshEquipmentInventory()
    {
        if (EquipmentInventoryList is null) return;
        _refreshingEquipmentInventory = true;
        try
        {
            var document = Session?.Document;
            var bankChoices = Enum.GetValues<EquipmentSlot>().Where(slot => slot != EquipmentSlot.Weapon)
                .Select(slot => new EquipmentBankChoice(slot, UiLanguage.Get(slot.ToString()))).ToArray();
            EquipmentBankInput.ItemsSource = bankChoices;
            EquipmentBankInput.SelectedItem = bankChoices.FirstOrDefault(choice => choice.Slot == _equipmentInventorySlot);
            EquipmentBankInput.IsVisible = _equipmentInventorySlot != EquipmentSlot.Weapon;
            EquipmentInventoryTitle.Text = UiLanguage.Get(_equipmentInventorySlot.ToString());
            var recipients = document?.Characters.Select(character => new EquipmentRecipientChoice(character.Id,
                CharacterCatalog.Get(character.Id, UiLanguage.Current))).ToArray() ?? [];
            EquipmentRecipientInput.ItemsSource = recipients;
            EquipmentRecipientInput.SelectedItem = recipients.FirstOrDefault(choice => choice.Id == _equipmentRecipient) ?? recipients.FirstOrDefault();
            _equipmentRecipient = (EquipmentRecipientInput.SelectedItem as EquipmentRecipientChoice)?.Id;
            var items = document?.InventoryEquipment(_equipmentInventorySlot) ?? [];
            string query = EquipmentInventorySearch.Text?.Trim() ?? "";
            var duplicates = items.GroupBy(item => item.Name).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
            var rows = items.Select(item => new InventoryRow(item.Index,
                duplicates.Contains(item.Name) ? $"{item.Name} · #{item.Index}" : item.Name))
                .Where(row => row.Label.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            EquipmentInventoryList.ItemsSource = rows;
            EquipmentInventoryList.SelectedItem = rows.FirstOrDefault(row => row.Index == _equipmentInventorySelections.GetValueOrDefault(_equipmentInventorySlot))
                ?? rows.FirstOrDefault();
            int? selected = (EquipmentInventoryList.SelectedItem as InventoryRow)?.Index;
            _equipmentInventorySelections[_equipmentInventorySlot] = selected;
            var owned = selected is { } index ? document?.GetInventoryEquipment(_equipmentInventorySlot, index) : null;
            EquipmentInventoryCount.Text = $"{items.Count}/{InventoryCatalog.Capacity}";
            OwnedEquipmentCard.IsVisible = owned is not null;
            OwnedEquipmentDetails.IsVisible = owned is not null;
            OwnedEquipmentSockets.Text = owned?.GemSlotCount.ToString();
            OwnedEquipmentFixedGems.Text = FixedGemNames(owned?.FixedGemIds ?? []);
            OwnedEquipmentUsers.Text = owned?.EquippedBy.Count > 0
                ? string.Join(", ", owned.EquippedBy.Select(id => CharacterCatalog.Get(id, UiLanguage.Current))) : UiLanguage.Get("None");
            EquipmentFavoriteInput.IsChecked = owned?.Favorite ?? false;
            EquipmentFavoriteInput.IsEnabled = owned?.CanEditFavorite == true;
            DeleteEquipmentButton.IsEnabled = owned?.CanDelete == true;
            var definitions = document?.CreatableEquipment(_equipmentInventorySlot) ?? [];
            FillEquipmentButton.IsEnabled = document?.CanEditInventoryEquipment == true && _equipmentRecipient is { } characterId
                && definitions.Any(item => item.Characters.Contains(characterId) && !items.Any(ownedItem => ownedItem.ItemId == item.Id));
            RefreshEquipmentDefinitions();
        }
        finally { _refreshingEquipmentInventory = false; }
    }

    private void RefreshEquipmentDefinitions()
    {
        int? previous = (EquipmentDefinitionInput.SelectedItem as InventoryChoice)?.Id;
        string query = EquipmentDefinitionSearch.Text?.Trim() ?? "";
        var definitions = (Session?.Document.CreatableEquipment(_equipmentInventorySlot) ?? [])
            .Where(item => _equipmentRecipient is { } id && item.Characters.Contains(id)
                && item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        var duplicates = definitions.GroupBy(item => item.Name).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        var choices = definitions.Select(item => new InventoryChoice(item.Id,
            duplicates.Contains(item.Name) ? $"{item.Name} ({item.Id})" : item.Name)).ToArray();
        EquipmentDefinitionInput.ItemsSource = choices;
        EquipmentDefinitionInput.SelectedItem = choices.FirstOrDefault(choice => choice.Id == previous) ?? choices.FirstOrDefault();
        RefreshNewEquipment();
    }

    private void RefreshNewEquipment()
    {
        var definition = EquipmentDefinitionInput.SelectedItem is InventoryChoice choice ? EquipmentDefinitions.Find(choice.Id) : null;
        NewEquipmentSockets.Text = definition?.GemSlotCount.ToString() ?? UiLanguage.Get("None");
        NewEquipmentFixedGems.Text = FixedGemNames(definition is null ? [] : Enumerable.Range(1, 3).Select(definition.FixedGem).Where(id => id > 0));
        CreateEquipmentButton.IsEnabled = Session?.Document.CanEditInventoryEquipment == true && definition is not null;
    }

    private static string FixedGemNames(IEnumerable<int> ids)
    {
        string[] names = ids.Select(EquipmentCatalog.ItemName).ToArray();
        return names.Length == 0 ? UiLanguage.Get("None") : string.Join(", ", names);
    }

    private void EquipmentInventoryList_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingEquipmentInventory) return;
        _equipmentInventorySelections[_equipmentInventorySlot] = (EquipmentInventoryList.SelectedItem as InventoryRow)?.Index;
        RefreshEquipmentInventory();
    }

    private void EquipmentBank_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingEquipmentInventory || EquipmentBankInput.SelectedItem is not EquipmentBankChoice bank) return;
        _equipmentInventorySlot = bank.Slot;
        RefreshEquipmentInventory();
    }

    private void EquipmentRecipient_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingEquipmentInventory) return;
        _equipmentRecipient = (EquipmentRecipientInput.SelectedItem as EquipmentRecipientChoice)?.Id;
        RefreshEquipmentInventory();
    }

    private void EquipmentInventorySearch_Changed(object? sender, TextChangedEventArgs e)
    { if (!_refreshingEquipmentInventory) RefreshEquipmentInventory(); }

    private void EquipmentDefinitionSearch_Changed(object? sender, TextChangedEventArgs e)
    { if (!_refreshingEquipmentInventory) RefreshEquipmentInventory(); }

    private void EquipmentDefinition_Changed(object? sender, SelectionChangedEventArgs e)
    { if (!_refreshingEquipmentInventory && NewEquipmentSockets is not null) RefreshNewEquipment(); }

    private void EquipmentFavorite_Changed(object? sender, RoutedEventArgs e)
    {
        if (_refreshingEquipmentInventory || Session is null || _equipmentInventorySelections.GetValueOrDefault(_equipmentInventorySlot) is not { } index) return;
        ApplyEquipmentInventoryEdit(() => Session.Document.GetInventoryEquipment(_equipmentInventorySlot, index).SetFavorite(EquipmentFavoriteInput.IsChecked == true));
    }

    private void CreateEquipment_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is null || EquipmentDefinitionInput.SelectedItem is not InventoryChoice choice) return;
        ApplyEquipmentInventoryEdit(() =>
        {
            var created = Session.Document.AddEquipment(choice.Id);
            _equipmentInventorySelections[_equipmentInventorySlot] = created.Index;
            EquipmentInventorySearch.Text = "";
        });
    }

    private void FillEquipment_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is null || _equipmentRecipient is not { } id) return;
        ApplyEquipmentInventoryEdit(() => Session.Document.FillMissingEquipment(_equipmentInventorySlot, id));
    }

    private void DeleteEquipment_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is null || _equipmentInventorySelections.GetValueOrDefault(_equipmentInventorySlot) is not { } index) return;
        ApplyEquipmentInventoryEdit(() => Session.Document.GetInventoryEquipment(_equipmentInventorySlot, index).Delete());
    }

    private void ApplyEquipmentInventoryEdit(Action edit)
    {
        try
        {
            _refreshingEquipmentInventory = true;
            edit();
            ShowStatus(null);
        }
        catch (ArgumentException) { ShowStatus(UiLanguage.Get("InvalidInventory")); }
        finally { _refreshingEquipmentInventory = false; }
        RefreshEquipmentInventory();
        RefreshEquipment();
    }
}
