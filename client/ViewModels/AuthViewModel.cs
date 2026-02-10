using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public partial class AuthViewModel : ViewModelBase
{ 
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ConfirmedPassword { get; set; }
    
    private AuthModel model;

    public AuthViewModel(Client client)
    {
        model = new(client);
    }

    public AuthViewModel() { }
    
    [RelayCommand]
    public async void Login()
    {
        var request = new LoginRequest(Username, Password);
        Console.WriteLine("login");
        await model.Login(request);
    }
    
    [RelayCommand]
    public async void Register()
    {
        var request = new RegisterRequest(Username, Password);
        await model.Register(request);
        Console.WriteLine("reg");
    }
}