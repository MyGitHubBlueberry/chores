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
        (ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            switch (packet.code)
            {
                case OpCode.Login:
                    return await HandleLoginAsync(context, packet);
                case OpCode.Register:
                    return await ActAsync<RegisterRequest, Result<User>>
                        (context.Stream, packet, service.RegisterAsync);
                default:
                    return false;
            }
        }
        return false;
    }

    public OpCode[] GetHandledCodes()
    {
        return [ OpCode.Login, OpCode.Register ];
    }

    private async Task<bool> HandleLoginAsync(ClientContext context, ReadPacket packet)
    {
        LoginRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<LoginRequest>(packet.jsonData);
        }
        catch
        {
            return false;
        }
        Debug.Assert(request != null);

        var responce = await service.LoginAsync(request);
        if (responce.IsSuccess)
            context.CurrentUser = responce.Value;

        SendPacket<Result> sendPacket = new(packet.code, responce);
        await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
        return responce.IsSuccess;
    }

    public async Task<bool> ActAsync<T, Res>
        (NetworkStream stream, ReadPacket packet, Func<T, Task<Res>> func)
        where Res : Result
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

        var responce = await func.Invoke(request);

        SendPacket<Result> sendPacket = new(packet.code, responce);
        await PacketProtocol.SendPacketAsync(stream, sendPacket);
        return responce.IsSuccess;
    }
}
