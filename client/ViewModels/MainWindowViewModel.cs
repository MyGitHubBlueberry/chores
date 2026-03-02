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
    private readonly ConnectionViewModel connectionViewModel;
    [ObservableProperty] ViewModelBase currentView;
    [ObservableProperty] ViewModelBase floatingView;
    [ObservableProperty] ViewModelBase popupView;
    [ObservableProperty] bool isFloatingViewVisible;
    [ObservableProperty] bool isPopupViewVisible;
    public MainWindowViewModel(
        Client client, 
        ConnectionViewModel connectionViewModel, 
        AuthViewModel authViewModel,
        MyChoresViewModel myChoresViewModel,
        HomeViewModel homeViewModel)
    {
        this.client = client;
        this.connectionViewModel = connectionViewModel;
        this.authViewModel= authViewModel;
        this.myChoresViewModel = myChoresViewModel;
        this.connectionViewModel.OnConnectionSuccess += () =>
            CurrentView = this.authViewModel;
        this.authViewModel.OnLoginSuccess += () =>
            CurrentView = homeViewModel;
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
        CurrentView = this.connectionViewModel;
    }
    
    public MainWindowViewModel() {} //for previewer
}
