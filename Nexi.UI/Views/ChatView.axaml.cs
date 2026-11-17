using System.Collections.Specialized;

using Avalonia.Controls;
using Avalonia.Input;

using Nexi.UI.ViewModels;

namespace Nexi.UI.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ChatViewModel viewModel)
        {
            viewModel.Messages.CollectionChanged += OnMessagesChanged;
            viewModel.ScrollToBottom += ScrollToEnd;
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToEnd();

    private void ScrollToEnd() => MessagesScroll.ScrollToEnd();

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ChatViewModel viewModel)
        {
            e.Handled = true;
            if (viewModel.SendMessageCommand.CanExecute(null))
            {
                viewModel.SendMessageCommand.Execute(null);
            }
        }
    }
}
