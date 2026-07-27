using CommunityToolkit.Mvvm.Input;

namespace QuantHub.Desktop.ViewModels.Pages;

/// <summary>Lets the shell's single Refresh button re-trigger whichever page is currently shown,
/// without the shell needing to know each page's specific load method. IAsyncRelayCommand (not the
/// plain ICommand base) because [RelayCommand] on an async Task method generates exactly this type,
/// and property interface implementations in C# don't allow covariance.</summary>
public interface IRefreshablePage
{
    IAsyncRelayCommand RefreshCommand { get; }
}
