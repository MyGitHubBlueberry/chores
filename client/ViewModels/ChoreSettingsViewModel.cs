using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace client.ViewModels;

public partial class ChoreSettingsViewModel : ViewModelBase
{
    public event Action OnCloseSettingsRequested;
    [ObservableProperty] private string name;
    [ObservableProperty] private string description;

    [RelayCommand]
    private void CloseChoreSettings()
    {
        //reset unsaved stuff
        //fire an event
        OnCloseSettingsRequested.Invoke();
    }
}