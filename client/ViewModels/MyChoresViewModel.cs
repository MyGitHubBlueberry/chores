using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Networking;
using Shared.Database.Models;
using Shared.Networking.Packets;

namespace client.ViewModels;

public partial class MyChoresViewModel : ViewModelBase
{
    public event Action OnCreateChoreViewOpenRequested;
    public event Action<int> OnChoreManagementViewOpenRequested;
    [ObservableProperty]
    private ObservableCollection<ChoreMemberData> chores;

    private readonly MyChoresModel model;
    private readonly UserSessionStore session;

    public MyChoresViewModel(Client client, UserSessionStore session)
    {
        this.session = session;
        model = new MyChoresModel(client, session, newData =>
        {
            Dispatcher.UIThread.Post(() => 
            {
                Chores = new ObservableCollection<ChoreMemberData>(newData);
            });
        });
    }
    
    public void LoadChores()
    {
        if (session.IsLoggedIn)
        {
            Console.WriteLine("request started");
            _ = model.GetChoresWithPrivilegesAsync(new GetChoreNameToPrivilege(session.User.Id));
            Console.WriteLine("after request");
        }
    }
    
    [RelayCommand]
    private void OpenCreateChoreView()
    {
        Console.WriteLine("create chore pressed");
        OnCreateChoreViewOpenRequested?.Invoke();
    }
    
    [RelayCommand]
    private void CloseCreateChoreView()
    {
        Console.WriteLine("create chore pressed");
        OnCreateChoreViewOpenRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenChoreManagementView(int id)
    {
        //todo set id to the view
        OnChoreManagementViewOpenRequested?.Invoke(id);
    }
}