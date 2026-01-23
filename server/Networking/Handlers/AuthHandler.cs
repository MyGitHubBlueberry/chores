using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class AuthHandler(UserService service) : IPacketHandler
{
    public async Task<bool> HandleAsync
        (NetworkStream stream, ReadPacket packet, CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            switch (packet.code)
            {
                case OpCode.Login:
                    {
                        var request = JsonSerializer.Deserialize<LoginRequest>(packet.jsonData);
                        Debug.Assert(request != null);

                        var responce = await service.LoginAsync(request);

                        RegisterResponce data = new(responce);
                        SendPacket<RegisterResponce> sendPacket = new(packet.code, data);
                        await PacketProtocol.SendPacketAsync(stream, sendPacket);
                        return responce.IsSuccess;
                    }
                case OpCode.Register:
                    {
                        await Test<RegisterRequest>(stream, packet, service.RegisterAsync);
                    }
                default:
                    return false;
            }
        }
        return false;
    }

    //todo: think about it
    public async Task<bool> Test<T>(NetworkStream stream, ReadPacket packet, Func<T, Task<Result>> func)
    {
        T? request;
        try
        {
            request = JsonSerializer.Deserialize<T>(packet.jsonData);
        }
        catch
        {
            return false;
        }

        Debug.Assert(request != null);

        //different function signatures
        var responce = await func.Invoke(request);

        //different result return types
        SendPacket<Result> sendPacket = new(packet.code, responce);
        await PacketProtocol.SendPacketAsync(stream, sendPacket);
        return responce.IsSuccess;
    }
}
