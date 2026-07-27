using CommunityToolkit.Mvvm.ComponentModel;

namespace QuantHub.Desktop.ViewModels.Pages;

/// <summary>Placeholder for pages not yet built (Analyst/Peers/Insider/Market Pulse/AI Research
/// land in Phase 3/4). One shared instance whose Message is swapped based on the selected nav item.</summary>
public sealed partial class ComingSoonViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = "";
}
