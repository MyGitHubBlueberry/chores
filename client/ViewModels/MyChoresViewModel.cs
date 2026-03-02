using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Database.Models;

namespace client.ViewModels;

public partial class MyChoresViewModel : ViewModelBase
{
    public event Action OnCreateChoreViewOpenRequested;
    [ObservableProperty]
    private ObservableCollection<Chore> chores;

    public MyChoresViewModel()
    {
        chores =
        [
            new Chore()
            {
                Title = "test"
            },
            new Chore()
            {
                Title = "test2",
            }
        ];
    }
    
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