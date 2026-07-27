using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace QuantHub.Desktop;

public partial class SmokeTestWindow : Window
{
    public SmokeTestWindow() => AvaloniaXamlLoader.Load(this);
}
