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
        choreSettingsViewModel.PreviousChore = chore;
        choreSettingsViewModel.ChoreName = chore.Title;
        choreSettingsViewModel.Description = chore.Description;
        choreSettingsViewModel.StartMDY = chore.StartDate;
        choreSettingsViewModel.StartHM =
            TimeSpan.FromHours(chore.StartDate.Hour) + TimeSpan.FromMinutes(chore.StartDate.Minute);
        if (chore.EndDate.HasValue)
        {
            choreSettingsViewModel.EndHM = TimeSpan.FromHours(chore.EndDate.Value.Hour) + TimeSpan.FromMinutes(chore.EndDate.Value.Minute);
            choreSettingsViewModel.EndMDY = chore.EndDate.Value.Date;
        }

        choreSettingsViewModel.IntervalDay = chore.Interval.Days;
        choreSettingsViewModel.IntervalHour = chore.Interval.Hours;
        choreSettingsViewModel.IntervalMinute = chore.Interval.Minutes;
        choreSettingsViewModel.EntryDurationDay = chore.Duration.Days;
        choreSettingsViewModel.EntryDurationHour = chore.Duration.Hours;
        choreSettingsViewModel.EntryDurationMinute = chore.Duration.Minutes;
    }

    [RelayCommand]
    private void Delete() => _ = model.RequestDeleteChoreAsync(DeleteCallback);

    private void DeleteCallback(Result result) => OnChoreDeletionComplete?.Invoke(result);
}