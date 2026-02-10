using CommunityToolkit.Mvvm.ComponentModel;
using Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public partial class LoginViewModel : ViewModelBase
{ 
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ConfirmedPassword { get; set; }
    private LoginModel model = new();

    public void LoginClick()
    {
        var request = new LoginRequest(Username, Password);
        model.Login(request);
    }
}