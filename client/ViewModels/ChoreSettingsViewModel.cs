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
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Description is required")]
    private string description;

    public ChoreSettingsViewModel()
    {
        ValidateAllProperties();
    }
    
    [RelayCommand]
    private void CloseChoreSettings()
    {
        //reset unsaved stuff
        //fire an event
        OnCloseSettingsRequested.Invoke();
    }
}