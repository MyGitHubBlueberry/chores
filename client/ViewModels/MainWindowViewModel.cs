using System;
using Avalonia.Controls;
using Avalonia.Metadata;
using client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Networking;

namespace client.ViewModels;
//USE PROPERTIES OR UI WILL NOT REACT
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly Client client;
    private readonly AuthViewModel authViewModel;
    private readonly MyChoresViewModel myChoresViewModel;
    [ObservableProperty] ViewModelBase currentView;
    [ObservableProperty] ViewModelBase floatingView;
    [ObservableProperty] ViewModelBase popupView;
    [ObservableProperty] bool isFloatingViewVisible;
    [ObservableProperty] bool isPopupViewVisible;
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
        myChoresViewModel.OnCreateChoreViewOpenRequested += () =>
        {
            Console.WriteLine("you should see floating window now");
            var createChoreViewModel = new CreateChoreViewModel(client);
            FloatingView = createChoreViewModel;
            IsFloatingViewVisible = true;
            createChoreViewModel.OnCreateChoreRequested += () =>
            {
                PopupView = createChoreViewModel.PopupViewModel;
                IsFloatingViewVisible = false;
                IsPopupViewVisible = true;
            };
            createChoreViewModel.SettingsViewModel.OnCloseSettingsRequested += () =>
            {
                IsFloatingViewVisible = false;
                FloatingView = null;
            };
            createChoreViewModel.PopupViewModel.OnPopupRead += success =>
            {
                IsPopupViewVisible = false;
                if (success)
                {
                    IsFloatingViewVisible = false;
                    FloatingView = null;
                    PopupView = null;
                }
                else
                {
                    IsFloatingViewVisible = true;
                }
            };
        };
        CurrentView = connectionVm;
    }
    
    public MainWindowViewModel() {} //for previewer
}
