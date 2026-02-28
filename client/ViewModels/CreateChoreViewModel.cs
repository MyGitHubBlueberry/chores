using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Networking;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public partial class CreateChoreViewModel : ViewModelBase
{
    public Action OnCreateChoreRequested;
    
    [ObservableProperty]
    private ChoreSettingsViewModel settingsViewModel;
    [ObservableProperty]
    private CreateChorePopupViewModel popupViewModel;
    private CreateChoreModel model;
    
    public CreateChoreViewModel(Client client)
    {
        model = new(client);
        SettingsViewModel = new ChoreSettingsViewModel(client);
        PopupViewModel = new CreateChorePopupViewModel();
    }

    [RelayCommand]
    private void SendCreateChoreRequest()
    {
        OnCreateChoreRequested?.Invoke();
        PopupViewModel.OpenLoading();
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

    public void CreateChoreCallback(Result<Chore> result)
    {
        Console.Write("Received callback: ");
        Console.WriteLine(result.IsSuccess 
            ? "chore created successfully"
            : result.ErrorMessage);
        //close on success and show that chore was created successfully
        
        //on fail show message and revalidate all fields
        if (result.IsSuccess)
        {
            PopupViewModel.OpenSuccess();
        }
        else
        {
            Dispatcher.UIThread.Post(ValidateAllProperties);
            PopupViewModel.OpenError(result.ErrorMessage);
        }

    }
}