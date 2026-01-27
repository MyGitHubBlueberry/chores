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

public class UserHandler(UserService service) : PacketHandler
{
    protected override async Task<bool> HandleCodesAsync(ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        switch (packet.code)
        {
            case OpCode.GetAssociatedLogs:
                return await HandlePacketAsync<GetLogsByUserIdRequest, ICollection<ChoreLog>>
                    (context, packet, (req) =>
                            service.GetAssociatedLogsByIdAsync(req.UserId), token);
            case OpCode.GetMemberships:
                return await HandlePacketAsync<GetMembershipsByIdRequest, ICollection<ChoreMember>>
                    (context, packet, (req) =>
                            service.GetMembershipsByIdAsync(req.UserId), token);
            case OpCode.GetOwnedChores:
                return await HandlePacketAsync<GetOwnedChoresByIdRequest, ICollection<Chore>>
                    (context, packet, (req) =>
                            service.GetOwnedChoresByIdAsync(req.UserId), token);
            case OpCode.GetUserByName:
                return await HandlePacketAsync<GetUserByNameRequest, User>
                    (context, packet, (req) =>
                            service.GetByNameAsync(req.Username), token);
            case OpCode.GetUserById:
                return await HandlePacketAsync<GetUserByIdRequest, User>
                    (context, packet, (req) =>
                            service.GetByIdAsync(req.UserId), token);
            case OpCode.DeleteUser:
                if (context.CurrentUser is null)
                {
                    SendPacket<Result> sendPacket =
                        new(packet.code, Result.Fail(ServiceError.Conflict, "Log in first."));
                    await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
                    return true;
                }
                return await HandlePacketAsync<DeleteUserRequest>
                    (context, packet, async req => {
                        var result = await service.DeleteUserAsync
                            (context.CurrentUser.Id, context.CurrentUser.Id);
                        if (result.IsSuccess) context.CurrentUser = null;
                        return result;
                    }, token);

            default:
                return false;
        }
    }

    public override OpCode[] GetHandledCodes()
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
}
