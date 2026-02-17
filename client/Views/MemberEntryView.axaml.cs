using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace client.Views;

public partial class MemberEntryView : UserControl
{
    public MemberEntryView()
    {
        InitializeComponent();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        ContextMenu.Open((Control)sender);
    }
}