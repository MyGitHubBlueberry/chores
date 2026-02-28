using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Networking;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public partial class CreateChoreViewModel : ViewModelBase
{
    [ObservableProperty]
    private ChoreSettingsViewModel settingsViewModel;
    private CreateChoreModel model;
    
    public CreateChoreViewModel(Client client)
    {
        model = new(client);
        SettingsViewModel = new ChoreSettingsViewModel(client);
    }

    [RelayCommand]
    private void SendCreateChoreRequest()
    {
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
    }
}