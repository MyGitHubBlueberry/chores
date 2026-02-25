using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace client.ViewModels;

public partial class ChoreSettingsViewModel : ViewModelBase
{
    public event Action OnCloseSettingsRequested;
    
    [ObservableProperty] 
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Chore name is required")]
    private string name;
    [ObservableProperty]
    private string description;

    [ObservableProperty] private DateTimeOffset? startMDY;
    [ObservableProperty] private TimeSpan? startHM;
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
}