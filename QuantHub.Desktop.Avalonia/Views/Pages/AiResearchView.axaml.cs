using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using QuantHub.Desktop.ViewModels.Pages;

namespace QuantHub.Desktop.Views.Pages;

public partial class AiResearchView : UserControl
{
    private readonly ScrollViewer _chatScrollViewer;

    public AiResearchView()
    {
        AvaloniaXamlLoader.Load(this);
        _chatScrollViewer = this.FindControl<ScrollViewer>("ChatScrollViewer")!;
        this.FindControl<TextBox>("MessageInput")!.KeyDown += OnMessageInputKeyDown;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnMessageInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is AiResearchViewModel vm && vm.SendCommand.CanExecute(null))
        {
            vm.SendCommand.Execute(null);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is AiResearchViewModel newVm) newVm.Messages.CollectionChanged += OnMessagesChanged;
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

    private void ScrollToEnd() => Dispatcher.UIThread.InvokeAsync(() =>
        _chatScrollViewer.Offset = new Vector(0, _chatScrollViewer.Extent.Height));
}
