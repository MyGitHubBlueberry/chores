using CommunityToolkit.Mvvm.ComponentModel;
using Networking;

namespace client.ViewModels;

public partial class CreateChoreViewModel : ViewModelBase
{
    [ObservableProperty]
    private ChoreSettingsViewModel settingsViewModel;
    
    public CreateChoreViewModel(Client client)
    {
        SettingsViewModel = new ChoreSettingsViewModel(client);
    }
}