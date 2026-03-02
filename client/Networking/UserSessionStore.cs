namespace Networking;

using Shared.Database.Models;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class UserSessionStore : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoggedIn))]
    private User? user;

    public bool IsLoggedIn => User != null;

    public void Login(User user)
    {
        User = user;
    }

    public void Logout()
    {
        User = null;
    }
}