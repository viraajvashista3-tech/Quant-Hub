using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace QuantHub.Desktop.Views.Controls;

public partial class ErrorCard : UserControl
{
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ErrorCard, string?>(nameof(Message));

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public ErrorCard()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
