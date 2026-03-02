using System;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;
using Networking;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public class AuthModel
{
    private readonly Client client;
    public event Action<User> OnLoginSuccess;

    public AuthModel(Client client)
    {
        this.client = client;
        client.PacketRecieved += OnPacketReceived;
    }

    private void OnPacketReceived(ReadPacket packet)
    {
        Console.WriteLine("Received packet");
        switch (packet.code)
        {
            case OpCode.Login:
                Console.WriteLine("Started deserialization");
                Result<User>? result = JsonSerializer.Deserialize<Result<User>>(packet.jsonData);
                Console.WriteLine("finished deserialization");
                Console.WriteLine("Result is null: " + (result is null));
                if (result.IsSuccess)
                {
                    Console.WriteLine("Client logged in successfully");
                    Console.WriteLine("user is null: " + (result.Value is null));
                    Console.WriteLine("Username is: " + result.Value?.Username);
                    OnLoginSuccess.Invoke(result.Value);
                }
                else
                {
                    Console.WriteLine("client failed to log in: " + result.ErrorMessage);
                }
                break;
            case OpCode.Register:
                Console.WriteLine("client received reg response");
                break;
        }
    }

    public async Task Login(LoginRequest request)
    {
        if (client.IsConnected)
        {
            await client.SendAsync(new SendPacket<LoginRequest>(OpCode.Login, request));
            Console.WriteLine("Sent login request");
        }
    }

    public async Task Register(RegisterRequest request)
    {
        if (client.IsConnected)
        {
            await client.SendAsync(new SendPacket<RegisterRequest>(OpCode.Register, request));
            Console.WriteLine("Sent reg request");
        }
    }
}