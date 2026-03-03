using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Networking;
using Shared.Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public partial class ChoreManagementViewModel : ViewModelBase
{
    private readonly Client client;
    private readonly UserSessionStore session;
    private ChoreManagementModel model;
    [ObservableProperty] private ChoreSettingsViewModel choreSettingsViewModel;
    
    public ChoreManagementViewModel(Client client, UserSessionStore session, ChoreSettingsViewModel choreSettingsViewModel)
    {
        this.client = client;
        this.session = session;
        this.choreSettingsViewModel = choreSettingsViewModel;
    }

    public void Initialize(int choreId)
    {
        model = new(client, choreId);
    }

    [RelayCommand]
    private void Delete() => _ = model.RequestDeleteChoreAsync(DeleteCallback);

    private void DeleteCallback(Result result)
    {
        
    }
}