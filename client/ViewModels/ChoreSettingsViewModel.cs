using CommunityToolkit.Mvvm.ComponentModel;

namespace client.ViewModels;

public partial class ChoreSettingsViewModel : ViewModelBase
{
    [ObservableProperty] private string name;
    [ObservableProperty] private string description;
}