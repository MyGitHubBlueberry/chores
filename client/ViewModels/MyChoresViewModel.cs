using System;
using CommunityToolkit.Mvvm.Input;

namespace client.ViewModels;

public partial class MyChoresViewModel : ViewModelBase
{
    public event Action OnCreateChoreRequested;
    
    [RelayCommand]
    private void CreateChore()
    {
        Console.WriteLine("create chore pressed");
        OnCreateChoreRequested.Invoke();
    }
}