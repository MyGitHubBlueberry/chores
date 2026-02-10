using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Networking;
using System;
using System.Threading.Tasks;

namespace client.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly Client client;
    
    [ObservableProperty] private string ipAddress = "127.0.0.1";
    [ObservableProperty] private int port = 7777;
    [ObservableProperty] private string statusMessage = "Enter Server Info";
    [ObservableProperty] private bool isConnecting = false;
    
    public event Action OnConnectionSuccess;

    public ConnectionViewModel(Client client)
    {
        this.client = client;
    }

    [RelayCommand]
    public async Task Connect()
    {
        if (IsConnecting) return;
        
        IsConnecting = true;
        StatusMessage = "Connecting...";

        try
        {
            await client.ConnectAsync(ipAddress, Port, 5);
            
            if (client.IsConnected)
            {
                StatusMessage = "Connected!";
                OnConnectionSuccess?.Invoke();
            }
            else
            {
                StatusMessage = "Failed to connect to the server.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }
}