using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class ChoreHandler(ChoreService service) : IPacketHandler
{
    public async Task<bool> HandleAsync(ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            if (context.CurrentUser is null)
                return false;
            switch (packet.code)
            {
                case OpCode.CreateChore:
                    return await Handle<CreateChoreRequest, Chore>
                        (context, packet, req =>
                             service.CreateChoreAsync
                                 (context.CurrentUser.Id, req, token), token);
                case OpCode.DeleteChore:
                    return await Handle<DeleteChoreRequest>
                        (context, packet, req =>
                             service.DeleteChoreAsync
                                 (context.CurrentUser.Id, req.ChoreId, token), token);
                case OpCode.UpdateChoreDetails:
                    return await Handle<UpdateChoreDetailsRequest>
                        (context, packet, req =>
                             service.UpdateDetailsAsync
                                 (context.CurrentUser.Id, req, token), token);
                case OpCode.UpdateChoreSchedule:
                    return await Handle<UpdateChoreScheduleRequest>
                        (context, packet, req =>
                             service.UpdateScheduleAsync
                                 (context.CurrentUser.Id, req, token), token);
                case OpCode.PauseChore:
                    return await Handle<PauseChoreRequest>
                        (context, packet, req =>
                             service.PauseChoreAsync
                                 (context.CurrentUser.Id, req.ChoreId, token), token);
                case OpCode.UnpauseChore:
                    return await Handle<UnpauseChoreRequest>
                        (context, packet, req =>
                             service.UnpauseChoreAsync
                                 (context.CurrentUser.Id, req.ChoreId, token), token);
                default:
                    return false;
            }
        }
        return false;
    }

        private async Task<bool> Handle<Req, Res>
        (ClientContext context, ReadPacket packet, Func<Req, Task<Result<Res>>> func, CancellationToken token)
        where Req : Request
    {
        return await Handle(context, packet, func, token);
    }

        private async Task<bool> Handle<Req>
            (ClientContext context, ReadPacket packet, Func<Req, Task<Result>> func, CancellationToken token)
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
}
