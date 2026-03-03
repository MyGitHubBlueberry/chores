using System;
using Avalonia.Controls;
using Avalonia.Metadata;
using client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
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
        {
            this.myChoresViewModel.LoadChores();
            CurrentView = homeViewModel;
        };
        myChoresViewModel.OnCreateChoreViewOpenRequested += () =>
        {
            Console.WriteLine("you should see floating window now");
            var createChoreViewModel = App.Current.Services?.GetService<CreateChoreViewModel>();
            FloatingView = createChoreViewModel;
            IsFloatingViewVisible = true;
            createChoreViewModel.OnCreateChoreRequested += () => OpenPopup(createChoreViewModel);
            createChoreViewModel.SettingsViewModel.OnCloseSettingsRequested += CloseAndResetFloatingView;
            createChoreViewModel.PopupViewModel.OnPopupRead += OnPopupRead;
        };
        myChoresViewModel.OnChoreManagementViewOpenRequested += id =>
        {
            var managementView = App.Current.Services?.GetService<ChoreManagementViewModel>();
            managementView.Initialize(id);
            FloatingView = managementView;
            IsFloatingViewVisible = true;
            managementView.ChoreSettingsViewModel.OnCloseSettingsRequested += CloseAndResetFloatingView;
        };
        CurrentView = this.connectionViewModel;
    }

    private void OpenPopup(CreateChoreViewModel createChoreViewModel)
    {
        PopupView = createChoreViewModel.PopupViewModel;
        IsFloatingViewVisible = false;
        IsPopupViewVisible = true;
    }

    private void CloseAndResetFloatingView()
    {
        IsFloatingViewVisible = false;
        FloatingView = null;
    }

    private void OnPopupRead(bool success)
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
    }


    public MainWindowViewModel() {} //for previewer
}
