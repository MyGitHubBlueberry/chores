using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class ChoreMemberHandler(ChoreMemberService service) : PacketHandler
{
    protected override async Task<bool> HandleCodesAsync
        (ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        //todo: maybe send a responce
        if (context.CurrentUser is null)
            return false;
        switch (packet.code)
        {
            case OpCode.AddMember:
                return await HandlePacketAsync<AddMembersRequest>(context, packet, req =>
                        service.AddMembersAsync(context.CurrentUser.Id, req, token), token);
            case OpCode.DeleteMember:
                return await HandlePacketAsync<DeleteMemberRequest>(context, packet, req =>
                        service.DeleteMemberAsync(context.CurrentUser.Id, req, token), token);
            case OpCode.SetAdminStatus:
                return await HandlePacketAsync<SetAdminStatusRequest>(context, packet, req =>
                        service.SetAdminStatusAsync(context.CurrentUser.Id, req, token), token);
            default:
                return false;
        }
    }

    public override OpCode[] GetHandledCodes()
    {
        return [
            OpCode.AddMember,
            OpCode.DeleteMember,
            OpCode.SetAdminStatus,
        ];
    }
}
