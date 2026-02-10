using System;
using Avalonia.Controls;
using client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Networking;

namespace client.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly Client client;
    [ObservableProperty] ViewModelBase currentView;
    public MainWindowViewModel(Client client)
    {
        this.client = client;
        var connectionVm = new ConnectionViewModel(client);
        connectionVm.OnConnectionSuccess += () =>
            CurrentView = new AuthViewModel(client);
        CurrentView = connectionVm;
    }
    
    public MainWindowViewModel() {} //for previewer
}
