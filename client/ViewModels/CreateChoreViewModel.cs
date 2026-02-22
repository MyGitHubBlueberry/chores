using CommunityToolkit.Mvvm.ComponentModel;

namespace client.ViewModels;

public partial class CreateChoreViewModel : ViewModelBase
{
    [ObservableProperty]
    private ChoreSettingsViewModel settingsViewModel;
    
    public CreateChoreViewModel()
    {
        // Initialize it so it's ready for the view
        SettingsViewModel = new ChoreSettingsViewModel();
    }
}