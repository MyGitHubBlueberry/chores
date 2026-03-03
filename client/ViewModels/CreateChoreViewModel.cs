using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Networking;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public partial class CreateChoreViewModel : ViewModelBase
{
    public Action OnCreateChoreRequested;
    
    [ObservableProperty]
    private ChoreSettingsViewModel? settingsViewModel;
    [ObservableProperty]
    private CreateChorePopupViewModel? popupViewModel;
    private CreateChoreModel model;
    
    public CreateChoreViewModel(Client client)
    {
        model = new(client);
        SettingsViewModel = App.Current.Services?.GetService<ChoreSettingsViewModel>();
        PopupViewModel = App.Current.Services?.GetService<CreateChorePopupViewModel>();
    }

    [RelayCommand]
    private void SendCreateChoreRequest()
    {
        OnCreateChoreRequested?.Invoke();
        PopupViewModel?.OpenLoading();
        _ = model.CreateChoreAsync(new CreateChoreRequest(
            SettingsViewModel.ChoreName,
            SettingsViewModel.Description, 
            null,
                SettingsViewModel.StartDate,
                SettingsViewModel.EndDate,
                SettingsViewModel.Duration,
                SettingsViewModel.Interval
            ), CreateChoreCallback);
    }

    private void CreateChoreCallback(Result<Chore> result)
    {
        Console.Write("Received callback: ");
        Console.WriteLine(result.IsSuccess 
            ? "chore created successfully"
            : result.ErrorMessage);
        
        Dispatcher.UIThread.Post(() =>
        {
            if (result.IsSuccess)
            {
                PopupViewModel.OpenSuccess();
            }
            else
            {
                ValidateAllProperties();
                PopupViewModel.OpenError(result.ErrorMessage);
            }
        });
    }
}