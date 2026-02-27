using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace client.ViewModels;

public partial class ChoreSettingsViewModel : ViewModelBase
{
    public event Action OnCloseSettingsRequested;

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
    
    [Required(ErrorMessage = "Chore name is required")]
    [NotifyDataErrorInfo]
    [ObservableProperty] private string name;
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

    public ChoreSettingsViewModel()
    {
        ValidateAllProperties();
    }
    
    [RelayCommand]
    private void CloseChoreSettings()
    {
        OnCloseSettingsRequested.Invoke();
    }
    
    public static ValidationResult DateIsNotInThePast(DateTimeOffset? offset)
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
        {
            Console.WriteLine("instance.StartMDY is null");
            return ValidationResult.Success!;
        }

        if (time is null)
        {
            Console.WriteLine("time is null");   
            return ValidationResult.Success!;
        }
        if (instance.StartDate > DateTime.UtcNow)
        {
            Console.WriteLine("instance.StartMDY is null");
            return ValidationResult.Success!;
        }
        else
        {
            return new ValidationResult("Start date can't be in the past");
        }
        // return instance.StartDate > DateTime.UtcNow 
        //     ? ValidationResult.Success!
        //     : new ValidationResult("Start date can't be in the past");
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
}