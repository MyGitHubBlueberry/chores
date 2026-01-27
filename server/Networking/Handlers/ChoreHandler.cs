using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class ChoreHandler(ChoreService service) : PacketHandler
{
    protected override async Task<bool> HandleCodesAsync(ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        //todo: maybe send responce
        if (context.CurrentUser is null)
            return false;
        switch (packet.code)
        {
            case OpCode.CreateChore:
                return await HandlePacketAsync<CreateChoreRequest, Chore>
                    (context, packet, req =>
                         service.CreateChoreAsync
                             (context.CurrentUser.Id, req, token), token);
            case OpCode.DeleteChore:
                return await HandlePacketAsync<DeleteChoreRequest>
                    (context, packet, req =>
                         service.DeleteChoreAsync
                             (context.CurrentUser.Id, req.ChoreId, token), token);
            case OpCode.UpdateChoreDetails:
                return await HandlePacketAsync<UpdateChoreDetailsRequest>
                    (context, packet, req =>
                         service.UpdateDetailsAsync
                             (context.CurrentUser.Id, req, token), token);
            case OpCode.UpdateChoreSchedule:
                return await HandlePacketAsync<UpdateChoreScheduleRequest>
                    (context, packet, req =>
                         service.UpdateScheduleAsync
                             (context.CurrentUser.Id, req, token), token);
            case OpCode.PauseChore:
                return await HandlePacketAsync<PauseChoreRequest>
                    (context, packet, req =>
                         service.PauseChoreAsync
                             (context.CurrentUser.Id, req.ChoreId, token), token);
            case OpCode.UnpauseChore:
                return await HandlePacketAsync<UnpauseChoreRequest>
                    (context, packet, req =>
                         service.UnpauseChoreAsync
                             (context.CurrentUser.Id, req.ChoreId, token), token);
            default:
                return false;
        }
    }

    public override OpCode[] GetHandledCodes()
    {
        return [
            OpCode.CreateChore,
            OpCode.DeleteChore,
            OpCode.UpdateChoreDetails,
            OpCode.UpdateChoreSchedule,
            OpCode.PauseChore,
            OpCode.UnpauseChore
        ];
    }
}
