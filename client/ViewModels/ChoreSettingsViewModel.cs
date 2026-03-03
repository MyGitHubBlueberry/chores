using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Networking;
using Shared.Database.Models;
using Shared.Networking;

namespace client.ViewModels;

public partial class ChoreSettingsViewModel : ViewModelBase 
{
    public event Action OnCloseSettingsRequested;
    public ChoreDto? PreviousChore { get; set; }

    public DateTime? StartDate => StartMDY?.Date + StartHM;

    public DateTime? EndDate => EndMDY?.Date + EndHM;

    public TimeSpan? Duration =>
        new TimeSpan(EntryDurationDay, EntryDurationHour, EntryDurationMinute, 0) is var time && time == TimeSpan.Zero
            ? null
            : time;
    
    public TimeSpan? Interval =>
        new TimeSpan(IntervalDay, IntervalHour, IntervalMinute, 0) is var time && time == TimeSpan.Zero
            ? null
            : time;

    [CustomValidation(typeof(ChoreSettingsViewModel), nameof(ChoreIsUnique))]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Chore name is required")]
    [ObservableProperty] private string choreName;
    private Result? isChoreNameUnique = null;
    [ObservableProperty] private string description;
    
    [CustomValidation(typeof(ChoreSettingsViewModel), nameof(DateIsNotInThePast))]
    [NotifyDataErrorInfo]
    [ObservableProperty] private DateTimeOffset? startMDY;
    [CustomValidation(typeof(ChoreSettingsViewModel), nameof(StartTimeIsNotInThePast))]
    [NotifyDataErrorInfo]
    [ObservableProperty] private TimeSpan? startHM;

    [CustomValidation(typeof(ChoreSettingsViewModel), nameof(DateIsNotInThePast))]
    [CustomValidation(typeof(ChoreSettingsViewModel), nameof(EndDateIsAfterStartDate))]
    [NotifyDataErrorInfo]
    [ObservableProperty] private DateTimeOffset? endMDY;
    [CustomValidation(typeof(ChoreSettingsViewModel), nameof(EndTimeIsAfterStartDate))]
    [NotifyDataErrorInfo]
    [ObservableProperty] private TimeSpan? endHM;
    [ObservableProperty] private int entryDurationDay;
    [ObservableProperty] private int entryDurationHour;
    [ObservableProperty] private int entryDurationMinute;
    [ObservableProperty] private int intervalDay;
    [ObservableProperty] private int intervalHour;
    [ObservableProperty] private int intervalMinute;

    private readonly ChoreSettingsModel model;

    public ChoreSettingsViewModel(Client client)
    {
        model = new(client);
        model.OnNameVerificationResponseReceived += result =>
        {
            isChoreNameUnique = result;
            Dispatcher.UIThread.Post(() =>
            {
                ValidateProperty(ChoreName, nameof(ChoreName));
            });
        };
        ValidateAllProperties();
    }

    [RelayCommand]
    private void CloseChoreSettings()
    {
        OnCloseSettingsRequested.Invoke();
    }
    
    public static ValidationResult DateIsNotInThePast(DateTimeOffset? offset, ValidationContext ctx)
    {
        if (offset is null)
            return ValidationResult.Success!;
        return offset?.Date >= DateTime.Now.Date 
            ? ValidationResult.Success!
            : new ValidationResult("The date can't be in the past");
    }
    
    public static ValidationResult StartTimeIsNotInThePast(TimeSpan? time, ValidationContext ctx)
    {
        var instance = (ChoreSettingsViewModel)ctx.ObjectInstance;
        if (instance.StartMDY is null)
            return ValidationResult.Success!;
        if (time is null)
            return ValidationResult.Success!;
        if (instance.PreviousChore is not null)
            return ValidationResult.Success!;

        return instance.StartDate > DateTime.UtcNow 
            ? ValidationResult.Success!
            : new ValidationResult("Start date can't be in the past");
    }
    
    public static ValidationResult EndDateIsAfterStartDate(DateTimeOffset? offset, ValidationContext ctx)
    {
        if (offset is null)
            return ValidationResult.Success!;
        var vm = (ChoreSettingsViewModel)ctx.ObjectInstance;
        if (vm.StartMDY is null) 
            return ValidationResult.Success!;
        if (offset < vm.StartMDY)
            return new ValidationResult("End date can't be before start date");
        return ValidationResult.Success!;
    }
    
    public static ValidationResult EndTimeIsAfterStartDate(TimeSpan? time, ValidationContext ctx)
    {
        var instance = (ChoreSettingsViewModel)ctx.ObjectInstance;
        if (instance.StartDate is null)
            return ValidationResult.Success!;
        if (time is null)
            return ValidationResult.Success!;
        return instance.EndDate > instance.StartDate
            ? ValidationResult.Success!
            : new ValidationResult("End date should be after start date");
    }

    public static ValidationResult ChoreIsUnique(string? name, ValidationContext ctx)
    {
        if (name is null)
            return ValidationResult.Success!;
        
        var instance = (ChoreSettingsViewModel)ctx.ObjectInstance;
        
        if (instance.PreviousChore is not null && name == instance.PreviousChore?.Title)
            return ValidationResult.Success!;

        if (instance.isChoreNameUnique is null)
        {
            _ = instance.model.IsChoreNameUnique(name);
            return ValidationResult.Success!;
        }

        var result = instance.isChoreNameUnique.IsSuccess
            ? ValidationResult.Success!
            : new ValidationResult(instance.isChoreNameUnique.ErrorMessage);
        
        instance.isChoreNameUnique = null;
        return result;
    }
}