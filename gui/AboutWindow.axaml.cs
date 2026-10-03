using Avalonia.Controls;
using Avalonia.Interactivity;

namespace XbdeEditor.Gui;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionValue.Text = typeof(AboutWindow).Assembly.GetName().Version?.ToString(3);
    }

    private async void OpenProject_Click(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("https://github.com/jinghaihan/xbde-save-editor"));
}
