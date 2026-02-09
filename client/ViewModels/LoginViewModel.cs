using CommunityToolkit.Mvvm.ComponentModel;

namespace client.ViewModels;

public partial class LoginViewModel : ViewModelBase
{ 
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ConfirmedPassword { get; set; }

    public void Login()
    {
        
    }

    public void Register()
    {
        
    }
}