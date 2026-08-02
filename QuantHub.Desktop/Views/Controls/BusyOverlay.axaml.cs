using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace QuantHub.Desktop.Views.Controls;

public partial class BusyOverlay : UserControl
{
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<BusyOverlay, bool>(nameof(IsBusy));

    public bool IsBusy
    {
        get => GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public BusyOverlay()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
