using System;
using System.Diagnostics;
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

    public AuthModel(Client client)
    {
        this.client = client;
        client.PacketRecieved += OnPacketRecieved;
    }

    private void OnPacketRecieved(ReadPacket packet)
    {
        Console.WriteLine("Recieved packet");
        switch (packet.code)
        {
            case OpCode.Login:
                var result = JsonSerializer.Deserialize<Result<User>>(packet.jsonData);
                if (result.IsSuccess)
                {
                    Console.WriteLine("Client logged in sucessfully");
                }
                else
                {
                    Console.WriteLine("client failed to log in: " + result.ErrorMessage);
                }
                break;
            case OpCode.Register:
                Console.WriteLine("client recieved reg responce");
                break;
            default:
                Console.WriteLine("invalid code");
                break;
        }
    }

    public async Task Login(LoginRequest request)
    {
        if (client.IsConnected)
        {
            await this.client.SendAsync(new SendPacket<LoginRequest>(OpCode.Login, request));
            Console.WriteLine("Sent login request");
        }
    }

    public async Task Register(RegisterRequest request)
    {
        if (client.IsConnected)
        {
            await this.client.SendAsync(new SendPacket<RegisterRequest>(OpCode.Register, request));
            Console.WriteLine("Sent reg request");
        }
    }
}