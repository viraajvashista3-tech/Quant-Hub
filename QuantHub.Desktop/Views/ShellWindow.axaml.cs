using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using QuantHub.Desktop.ViewModels;

namespace QuantHub.Desktop.Views;

public partial class ShellWindow : Window
{
    private readonly ScrollViewer _contentScrollViewer;

    public ShellWindow(ShellViewModel viewModel)
    {
        AvaloniaXamlLoader.Load(this);
        _contentScrollViewer = this.FindControl<ScrollViewer>("ContentScrollViewer")!;
        DataContext = viewModel;

        // Swapping ContentControl.Content leaves the ScrollViewer at its previous scroll offset,
        // so a page navigated to from partway down a taller page renders with its top clipped
        // behind the fixed header bar until the user manually scrolls up.
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.CurrentPage))
        {
            _contentScrollViewer.Offset = new Vector(0, 0);
        }
    }
}
