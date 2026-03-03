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
        ChoreSettingsViewModel.PreviousChore = chore;
        ChoreSettingsViewModel.ChoreName = chore.Title;
        ChoreSettingsViewModel.Description = chore.Description;
        ChoreSettingsViewModel.StartMDY = chore.StartDate;
        ChoreSettingsViewModel.StartHM =
            TimeSpan.FromHours(chore.StartDate.Hour) + TimeSpan.FromMinutes(chore.StartDate.Minute);
        if (chore.EndDate.HasValue)
        {
            ChoreSettingsViewModel.EndMDY = chore.EndDate.Value.Date;
            ChoreSettingsViewModel.EndHM = TimeSpan.FromHours(chore.EndDate.Value.Hour) + TimeSpan.FromMinutes(chore.EndDate.Value.Minute);
        }
        ChoreSettingsViewModel.IntervalDay = chore.Interval.Days;
        ChoreSettingsViewModel.IntervalHour = chore.Interval.Hours;
        ChoreSettingsViewModel.IntervalMinute = chore.Interval.Minutes;
        ChoreSettingsViewModel.EntryDurationDay = chore.Duration.Days;
        ChoreSettingsViewModel.EntryDurationHour = chore.Duration.Hours;
        ChoreSettingsViewModel.EntryDurationMinute = chore.Duration.Minutes;
    }

    [RelayCommand]
    private void Delete() => _ = model.RequestDeleteChoreAsync(DeleteCallback);

    private void DeleteCallback(Result result) => OnChoreDeletionComplete?.Invoke(result);
}