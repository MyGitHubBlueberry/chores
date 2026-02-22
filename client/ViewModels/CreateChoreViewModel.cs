using CommunityToolkit.Mvvm.ComponentModel;

namespace client.ViewModels;

public partial class CreateChoreViewModel : ViewModelBase
{
    [ObservableProperty]
    private ChoreSettingsViewModel settingsViewModel;
    
    public CreateChoreViewModel()
    {
        SettingsViewModel = new ChoreSettingsViewModel();
    }
}