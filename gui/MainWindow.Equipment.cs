using Avalonia.Controls;
using XbdeEditor.Core;
using XbdeEditor.Gui.Localization;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private sealed record GemChoice(int? Index, string Label);
    private sealed class EquipmentSocketRow
    {
        public EquipmentSocketRow(EquipmentRecord equipment, GemSocketRecord socket)
        {
            Equipment = equipment;
            Socket = socket;
            Choices = new[] { new GemChoice(null, UiLanguage.Get("None")) }
                .Concat(socket.AvailableGems.Select(gem => new GemChoice(gem.Index, gem.Label))).ToArray();
            Selected = Choices.FirstOrDefault(choice => choice.Index == socket.GemIndex) ?? Choices[0];
        }
        public GemSocketRecord Socket { get; }
        public EquipmentRecord Equipment { get; }
        public string Label => $"{UiLanguage.Get("GemSocket")} {Socket.Index}";
        public bool CanEdit => Socket.CanEdit;
        public bool ReadOnly => !CanEdit;
        public bool IsFixed => Socket.FixedItemId is not null;
        public string Value => Socket.Name ?? UiLanguage.Get("None");
        public string FixedLabel => UiLanguage.Get("FixedGem");
        public GemChoice[] Choices { get; }
        public GemChoice Selected { get; }
    }
    private sealed record EquipmentRow(EquipmentRecord Equipment, EquipmentSocketRow[] Sockets)
    {
        public string Title => UiLanguage.Get(Equipment.Slot.ToString());
        public string Name => Equipment.Name;
        public bool NoSockets => Sockets.Length == 0;
    }
    private bool _refreshingEquipment;

    public void ShowEquipment()
    {
        ShowCharacters();
        CharacterNavigation.SelectedIndex = 3;
    }

    private void RefreshEquipment()
    {
        if (EquipmentList is null) return;
        _refreshingEquipment = true;
        try
        {
            EquipmentList.ItemsSource = _character?.Equipment.Select(equipment => new EquipmentRow(equipment,
                equipment.GemSockets.Select(socket => new EquipmentSocketRow(equipment, socket)).ToArray())).ToArray() ?? [];
        }
        finally { _refreshingEquipment = false; }
    }

    private void EquipmentGem_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingEquipment || sender is not ComboBox { Tag: EquipmentSocketRow row, SelectedItem: GemChoice choice }
            || !row.CanEdit || choice.Index == row.Socket.GemIndex || !CommitProgressionDraft()) return;
        try
        {
            row.Equipment.SetGems(row.Equipment.GemSockets
                .Select(socket => socket.Index == row.Socket.Index ? choice.Index : socket.GemIndex).ToArray());
            RefreshCharacters();
            ShowStatus(null);
        }
        catch (ArgumentException)
        {
            RefreshEquipment();
            ShowStatus(UiLanguage.Get("InvalidGem"));
        }
    }
}
