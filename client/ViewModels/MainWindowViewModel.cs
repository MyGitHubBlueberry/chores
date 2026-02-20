using System;
using Avalonia.Controls;
using Avalonia.Metadata;
using client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Networking;

namespace client.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly Client client;
    private readonly AuthViewModel authViewModel;
    [ObservableProperty] ViewModelBase currentView;
    public MainWindowViewModel(Client client)
    {
        this.client = client;
        var connectionVm = new ConnectionViewModel(client);
        authViewModel= new AuthViewModel(client);
        connectionVm.OnConnectionSuccess += () =>
            CurrentView = authViewModel;
        authViewModel.OnLoginSuccess += () =>
            CurrentView = new HomeViewModel();
        CurrentView = connectionVm;
    }
    
    public MainWindowViewModel() {} //for previewer
}
