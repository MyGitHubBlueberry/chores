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
    private readonly MyChoresViewModel myChoresViewModel;
    [ObservableProperty] ViewModelBase currentView;
    [ObservableProperty] ViewModelBase floatingView;
    [ObservableProperty] bool isFloatingViewVisible;
    public MainWindowViewModel(Client client)
    {
        this.client = client;
        var connectionVm = new ConnectionViewModel(client);
        authViewModel= new AuthViewModel(client);
        myChoresViewModel = new MyChoresViewModel();
        connectionVm.OnConnectionSuccess += () =>
            CurrentView = authViewModel;
        authViewModel.OnLoginSuccess += () =>
            CurrentView = new HomeViewModel(myChoresViewModel);
        myChoresViewModel.OnCreateChoreRequested += () =>
        {
            Console.WriteLine("you should see floating window now");
            var createChoreViewModel = new CreateChoreViewModel();
            FloatingView = createChoreViewModel;
            IsFloatingViewVisible = true;
            createChoreViewModel.SettingsViewModel.OnCloseSettingsRequested += () =>
            {
                IsFloatingViewVisible = false;
                FloatingView = null;
            };
        };
        CurrentView = connectionVm;
    }
    
    public MainWindowViewModel() {} //for previewer
}
