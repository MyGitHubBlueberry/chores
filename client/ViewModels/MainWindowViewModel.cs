using System;
using Avalonia.Controls;
using client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Networking;

namespace client.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] UserControl currentView = new LoginView();
}
