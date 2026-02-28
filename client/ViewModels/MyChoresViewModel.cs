using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace client.ViewModels;

public partial class MyChoresViewModel : ViewModelBase
{
    public event Action OnCreateChoreViewOpenRequested;

    [RelayCommand]
    private void OpenCreateChoreView()
    {
        Console.WriteLine("create chore pressed");
        OnCreateChoreViewOpenRequested.Invoke();
    }
    
    [RelayCommand]
    private void CloseCreateChoreView()
    {
        Console.WriteLine("create chore pressed");
        OnCreateChoreViewOpenRequested.Invoke();
    }

}