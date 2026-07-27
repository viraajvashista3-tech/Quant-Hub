using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace QuantHub.Desktop.Views.Controls;

public partial class MetricRow : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<MetricRow, string>(nameof(Label), defaultValue: "");

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<MetricRow, string>(nameof(Value), defaultValue: "");

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public MetricRow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
