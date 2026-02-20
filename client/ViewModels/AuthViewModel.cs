using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Networking;
using Shared.Database.Models;
using Shared.Networking.Packets;

namespace client.ViewModels;

public partial class AuthViewModel : ViewModelBase
{
    public event Action OnLoginSuccess;
    private string username;
    private string password;
    private string confirmedPassword;
    [ObservableProperty] private bool revealPassword;
    [ObservableProperty] private bool revealConfirmedPassword;
    
    [Required] [Length(4,15)] 
    [CustomValidation(typeof(AuthViewModel), nameof(ValidAscii))]
    [CustomValidation(typeof(AuthViewModel), nameof(HasNoSpaces))]
    public string Username
    {
        get => username;
        set => SetProperty(ref username, value, true);

    }

    [Required] [Length(4,15)]
    [CustomValidation(typeof(AuthViewModel), nameof(ValidAscii))]
    public string Password
    {
        get => password;
        set => SetProperty(ref password, value, true);

    }

    [Compare(nameof(Password), ErrorMessage = "Passwords don't match")]
    public string ConfirmedPassword
    {
        get => confirmedPassword;
        set => SetProperty(ref confirmedPassword, value, true);
    }


    private AuthModel model;

    public AuthViewModel(Client client)
    {
        model = new(client);
        model.OnLoginSuccess += () => OnLoginSuccess?.Invoke();
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
    
    [RelayCommand]
    public void TogglePasswordVisibility() =>
        RevealPassword = !RevealPassword;
    
    [RelayCommand]
    public void ToggleConfirmedPasswordVisibility() =>
        RevealConfirmedPassword = !RevealConfirmedPassword;

    public static ValidationResult ValidAscii(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) 
            return ValidationResult.Success!;

        if (!Ascii.IsValid(s))
            return new ValidationResult("Only standard characters allowed");

        return ValidationResult.Success!;
    }
    
    public static ValidationResult HasNoSpaces(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) 
            return ValidationResult.Success!;

        if (s.Contains(' '))
            return new ValidationResult("Spaces are forbidden");

        return ValidationResult.Success!;
    }
}