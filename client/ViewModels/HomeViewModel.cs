using CommunityToolkit.Mvvm.ComponentModel;

namespace client.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    [ObservableProperty] 
    private MyChoresViewModel myChoresVM;
    public HomeViewModel(MyChoresViewModel choresViewModel)
    {
        MyChoresVM = choresViewModel;
    }
    
    public HomeViewModel() {}
}