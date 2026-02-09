using Avalonia.Controls;
using client.Views;
using CommunityToolkit.Mvvm.ComponentModel;

namespace client.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] UserControl currentView = new Login();
}
