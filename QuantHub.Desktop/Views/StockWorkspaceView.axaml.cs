using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace QuantHub.Desktop.Views;

public partial class StockWorkspaceView : UserControl
{
    public StockWorkspaceView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
