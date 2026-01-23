using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class UserHandler(UserService service) : IPacketHandler
{
    public async Task<bool> HandleAsync
        (ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            switch (packet.code)
            {
                case OpCode.GetAssociatedLogs:
                    return await HandleGetAssociatedLogsAsync(context, packet, token);
                case OpCode.GetMemberships:
                    return await HandleGetMembershipsAsync(context, packet, token);
                case OpCode.GetOwnedChores:
                    return await HandleGetOwnedChoresAsync(context, packet, token);
                case OpCode.GetUserByName:
                    return await HandleGetUserByNameAsync(context, packet, token);
                case OpCode.GetUserById:
                    return await HandleGetUserByIdAsync(context, packet, token);
                case OpCode.DeleteUser:
                    return await HandleUserDeletionAsync(context, packet, token);
            }
        }
        return false;
    }

    private async Task<bool> HandleGetAssociatedLogsAsync
        (ClientContext context, ReadPacket packet, CancellationToken token)
    {
        var request = JsonSerializer.Deserialize<GetLogsByUserIdRequest>(packet.jsonData);
        Debug.Assert(request is not null);
        SendPacket<Result> sendPacket;
        var result = await service
            .GetAssociatedLogsByIdAsync(request.UserId);
        sendPacket = new(packet.code, result);
        await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
        return true;
    }

    private async Task<bool> HandleGetMembershipsAsync
        (ClientContext context, ReadPacket packet, CancellationToken token)
    {
        var request = JsonSerializer.Deserialize<GetMembershipsByIdRequest>(packet.jsonData);
        Debug.Assert(request is not null);
        SendPacket<Result> sendPacket;
        var result = await service
            .GetMembershipsByIdAsync(request.UserId);
        sendPacket = new(packet.code, result);
        await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
        return true;
    }

    private async Task<bool> HandleGetOwnedChoresAsync
        (ClientContext context, ReadPacket packet, CancellationToken token)
    {
        var request = JsonSerializer.Deserialize<GetOwnedChoresByIdRequest>(packet.jsonData);
        Debug.Assert(request is not null);
        SendPacket<Result> sendPacket;
        var result = await service
            .GetOwnedChoresByIdAsync(request.UserId);
        sendPacket = new(packet.code, result);
        await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
        return true;
    }

    private async Task<bool> HandleGetUserByNameAsync
        (ClientContext context, ReadPacket packet, CancellationToken token)
    {
        var request = JsonSerializer.Deserialize<GetUserByNameRequest>(packet.jsonData);
        Debug.Assert(request is not null);
        SendPacket<Result> sendPacket;
        var result = await service
            .GetByNameAsync(request.Username);
        sendPacket = new(packet.code, result);
        await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
        return true;
    }

    private async Task<bool> HandleGetUserByIdAsync
        (ClientContext context, ReadPacket packet, CancellationToken token)
    {
        var request = JsonSerializer.Deserialize<GetUserByIdRequest>(packet.jsonData);
        Debug.Assert(request is not null);
        SendPacket<Result> sendPacket;
        var result = await service
            .GetByIdAsync(request.UserId);
        sendPacket = new(packet.code, result);
        await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
        return true;
    }

    private async Task<bool> HandleUserDeletionAsync
        (ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        var request = JsonSerializer.Deserialize<DeleteUserRequest>(packet.jsonData);
        if (context.CurrentUser is null)
        {
            SendPacket<Result> sendPacket =
                new(packet.code, Result.Fail(ServiceError.Conflict, "Log in first."));
            await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
            return true;
        }
        var result = await service
            .DeleteUserAsync(context.CurrentUser.Id, context.CurrentUser.Id);
        if (result.IsSuccess)
        {
            context.CurrentUser = null;
            SendPacket<Result> sendPacket = new(packet.code, result);
            await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
        }
        return true;
    }
}
