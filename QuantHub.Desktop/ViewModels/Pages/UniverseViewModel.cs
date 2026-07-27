using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using QuantHub.Core.Models;
using QuantHub.Core.Universe;
using QuantHub.Desktop.Messages;

namespace QuantHub.Desktop.ViewModels.Pages;

public sealed partial class UniverseViewModel : ObservableObject
{
    public IReadOnlyList<UniverseSector> Sectors { get; } = UniverseData.AsSectors();

    [RelayCommand]
    private void SelectTicker(string ticker) =>
        WeakReferenceMessenger.Default.Send(new NavigateToTickerMessage(ticker));
}
