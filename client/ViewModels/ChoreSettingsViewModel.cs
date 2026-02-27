using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace client.ViewModels;

public partial class ChoreSettingsViewModel : ViewModelBase
{
    public event Action OnCloseSettingsRequested;

    public DateTime? StartDate
    {
        get => StartMDY?.DateTime + StartHM;
    }

    public DateTime? EndDate
    {
        get => EndMDY?.DateTime + EndHM;
    }

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
    [ObservableProperty] private TimeSpan? startHM;

    [CustomValidation(typeof(ChoreSettingsViewModel), nameof(DateIsNotInThePast))]
    [NotifyDataErrorInfo]
    [ObservableProperty] private DateTimeOffset? endMDY;
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

        return offset?.UtcDateTime > DateTime.UtcNow 
            ? ValidationResult.Success!
            : new ValidationResult("This can't be in the past");
    }
}