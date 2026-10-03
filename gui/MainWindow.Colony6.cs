using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace XbdeEditor.Gui;

public partial class MainWindow
{
    private void RefreshColony6()
    {
        var colony = Session?.Document.Colony6;
        Colony6Card.IsVisible = colony is not null;
        MaxColony6Button.IsEnabled = colony is { CanMaximize: true, IsMaximum: false };
        ToolTip.SetTip(MaxColony6Button, colony?.UnavailableReason);
        if (colony is null) return;
        Colony6HousingValue.Text = colony.Housing.ToString(CultureInfo.InvariantCulture);
        Colony6CommerceValue.Text = colony.Commerce.ToString(CultureInfo.InvariantCulture);
        Colony6NatureValue.Text = colony.Nature.ToString(CultureInfo.InvariantCulture);
        Colony6SpecialValue.Text = colony.Special.ToString(CultureInfo.InvariantCulture);
        Colony6OverallValue.Text = colony.OverallLevel.ToString(CultureInfo.InvariantCulture);
        Colony6DevelopmentValue.Text = $"{colony.Development}%";
        Colony6PopulationValue.Text = colony.Population.ToString(CultureInfo.InvariantCulture);
    }

    private void MaxColony6_Click(object? sender, RoutedEventArgs e)
    {
        if (Session is null) return;
        try
        {
            Session.Document.MaximizeColony6();
            RefreshColony6();
            ShowStatus(null);
        }
        catch (Exception error) when (IsExpected(error)) { ShowStatus(error.Message); }
    }
}
