using System.ComponentModel;
using System.Windows;
using QuantHub.Desktop.ViewModels;

namespace QuantHub.Desktop.Views;

public partial class ShellWindow : Window
{
    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Swapping ContentControl.Content leaves the ScrollViewer at its previous scroll offset,
        // so a page navigated to from partway down a taller page renders with its top clipped
        // behind the fixed header bar until the user manually scrolls up.
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.CurrentPage)) ContentScrollViewer.ScrollToHome();
    }
}
