using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Database.Models;
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
                    return await Handle<GetLogsByUserIdRequest, ICollection<ChoreLog>>
                        (context, packet, (req) =>
                                service.GetAssociatedLogsByIdAsync(req.UserId), token);
                case OpCode.GetMemberships:
                    return await Handle<GetMembershipsByIdRequest, ICollection<ChoreMember>>
                        (context, packet, (req) =>
                                service.GetMembershipsByIdAsync(req.UserId), token);
                case OpCode.GetOwnedChores:
                    return await Handle<GetOwnedChoresByIdRequest, ICollection<Chore>>
                        (context, packet, (req) =>
                                service.GetOwnedChoresByIdAsync(req.UserId), token);
                case OpCode.GetUserByName:
                    return await Handle<GetUserByNameRequest, User>
                        (context, packet, (req) =>
                                service.GetByNameAsync(req.Username), token);
                case OpCode.GetUserById:
                    return await Handle<GetUserByIdRequest, User>
                        (context, packet, (req) =>
                                service.GetByIdAsync(req.UserId), token);
                case OpCode.DeleteUser:
                    return await HandleUserDeletionAsync(context, packet, token);
            }
        }
        return false;
    }

    public OpCode[] GetHandledCodes()
    {
        return [
            OpCode.GetAssociatedLogs,
            OpCode.GetMemberships,
            OpCode.GetOwnedChores,
            OpCode.GetUserByName,
            OpCode.GetUserById,
            OpCode.DeleteUser
        ];
    }

    private async Task<bool> Handle<Req, Res>
        (ClientContext context, ReadPacket packet, Func<Req, Task<Result<Res>>> func, CancellationToken token)
        where Req : Request
    {
        var request = JsonSerializer.Deserialize<Req>(packet.jsonData);
        Debug.Assert(request is not null);
        SendPacket<Result> sendPacket;
        var result = await func.Invoke(request);
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
