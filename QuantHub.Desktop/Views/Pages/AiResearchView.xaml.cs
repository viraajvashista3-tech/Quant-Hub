using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using QuantHub.Desktop.ViewModels.Pages;

namespace QuantHub.Desktop.Views.Pages;

public partial class AiResearchView : UserControl
{
    public AiResearchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AiResearchViewModel oldVm) oldVm.Messages.CollectionChanged -= OnMessagesChanged;
        if (e.NewValue is AiResearchViewModel newVm) newVm.Messages.CollectionChanged += OnMessagesChanged;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollToEnd();

        // A streaming reply mutates its own Text property in place rather than raising collection
        // events, so without this the view stops following it partway through and the tail of the
        // answer renders below the fold until the user scrolls manually.
        if (e.NewItems is not null)
        {
            foreach (ChatMessageVm item in e.NewItems)
            {
                item.PropertyChanged += OnMessagePropertyChanged;
            }
        }
    }

    private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e) => ScrollToEnd();

    private void ScrollToEnd() => Dispatcher.InvokeAsync(() => ChatScrollViewer.ScrollToEnd());
}
