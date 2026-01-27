using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class ChoreQueueHandler(ChoreQueueService service) : IPacketHandler
{
    public async Task<bool> HandleAsync
        (ClientContext context, ReadPacket packet, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (context.CurrentUser is null)
                return false;
            switch (packet.code)
            {
                case OpCode.ExtendQueueFromDays:
                    return await Handle<ExtendQueueFromDaysRequest>
                        (context, packet, req => service.ExtendQueueFromDaysAsync
                             (req.ChoreId, req.Days, token), token);
                case OpCode.ExtendQueueFromEntryCount:
                    return await Handle<ExtendQueueFromEntryCountRequest>
                        (context, packet, req => service.ExtendQueueFromEntryCountAsync
                             (req.ChoreId, req.EntryCount, token), token);
                case OpCode.SwapQueueItems:
                    return await Handle<SwapQueueItemsRequest>
                        (context, packet, req => service.SwapQueueItemsAsync
                             (req.ChoreId, context.CurrentUser.Id, req.QueueItemAId, req.QueueItemBId, token), token);
                case OpCode.SwapQueueMembers:
                    return await Handle<SwapQueueMembersRequest>
                        (context, packet, req => service.SwapMembersInQueueAsync
                             (context.CurrentUser.Id, req.ChoreId, req.UserAId, req.UserBId, token), token);
                case OpCode.InsertQueueItem:
                    return await Handle<InsertQueueItemRequest>
                        (context, packet, req => service.InsertQueueEntryAsync
                             (req.ChoreId, context.CurrentUser.Id, req.Entry, token), token);
                case OpCode.InsertMemberInQueue:
                    return await Handle<InsertMemberInQueueRequest>
                        (context, packet, req => service.InsertMemberInQueueAsync
                             (req.ChoreId, context.CurrentUser.Id, req.MemberId, req.DesiredOrderRotationIdx, token), token);
                case OpCode.DeleteQueueItem:
                    return await Handle<DeleteQueueItemRequest>
                        (context, packet, req => service.DeleteQueueEntryAsync
                             (req.ChoreId, context.CurrentUser.Id, req.Entry, token), token);
                case OpCode.DeleteQueueMember:
                    return await Handle<DeleteMemberFromQueueRequest>
                        (context, packet, req => service.DeleteMemberFromQueueAsync
                             (req.ChoreId, context.CurrentUser.Id, req.MemberId, token), token);
                case OpCode.RegenerateQueue:
                    return await Handle<RegenerateQueueRequest>
                        (context, packet, req => service.RegenerateQueueAsync
                             (req.ChoreId, context.CurrentUser.Id, token), token);
                case OpCode.ChangeQueueItemInterval:
                    return await Handle<ChangeItemIntervalRequest>
                        (context, packet, req => service.ChangeQueueEntryIntervalAsync
                             (req.ChoreId, context.CurrentUser.Id, req.QueueEntryId, req.Interval, token), token);
                case OpCode.CompleteCurrentQueue:
                    return await Handle<CompleteCurrentItemRequest>
                        (context, packet, req => service.CompleteCurrentQueueEntryAsync
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
