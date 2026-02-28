using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace client.ViewModels;

public partial class CreateChorePopupViewModel : ViewModelBase
{
    public Action<bool> OnPopupRead;
    [ObservableProperty] private string? heading;
    [ObservableProperty] private string? body;
    [ObservableProperty] private bool canClose;
    private bool success;
    public void OpenSuccess()
    {
        Heading = "Chore created successfully!";
        CanClose = true;
        success = true;
    }

    public void OpenError(string? resultErrorMessage)
    {
        Heading = "Chore creation failed!";
        Body = resultErrorMessage;
        CanClose = true;
        success = false;
    }
    
    public void OpenLoading()
    {
        Heading = "Creating chore...";
        CanClose = false;
    }

    [RelayCommand]
    private void OkPressed()
    {
        Heading = Body = null;
        CanClose = false;
        OnPopupRead?.Invoke(success);
    }
}