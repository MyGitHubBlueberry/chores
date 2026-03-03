using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Networking;
using Shared.Database.Models;
using Shared.Networking;

namespace client.ViewModels;

public partial class ChoreManagementViewModel : ViewModelBase
{
    public Action<Result> OnChoreDeletionComplete;
    [ObservableProperty] private ChoreSettingsViewModel choreSettingsViewModel;
    
    private readonly Client client;
    private readonly UserSessionStore session;
    private ChoreManagementModel model;
    private ChoreDto chore;
    
    public ChoreManagementViewModel(Client client, UserSessionStore session, ChoreSettingsViewModel choreSettingsViewModel)
    {
        this.client = client;
        this.session = session;
        this.choreSettingsViewModel = choreSettingsViewModel;

    }

    public void Initialize(ChoreDto chore)
    {
        model = new(client, chore);
        choreSettingsViewModel.PreviousChoreName = chore.Title;
        choreSettingsViewModel.ChoreName = chore.Title;
        choreSettingsViewModel.Description = chore.Description;
    }

    [RelayCommand]
    private void Delete() => _ = model.RequestDeleteChoreAsync(DeleteCallback);

    private void DeleteCallback(Result result) => OnChoreDeletionComplete?.Invoke(result);
}