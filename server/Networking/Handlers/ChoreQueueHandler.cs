using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class ChoreQueueHandler(ChoreQueueService service) : PacketHandler
{
    protected override async Task<bool> HandleCodesAsync
        (ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        if (context.CurrentUser is null)
            return false;
        switch (packet.code)
        {
            case OpCode.ExtendQueueFromDays:
                return await HandlePacketAsync<ExtendQueueFromDaysRequest>
                    (context, packet, req => service.ExtendQueueFromDaysAsync
                         (req.ChoreId, req.Days, token), token);
            case OpCode.ExtendQueueFromEntryCount:
                return await HandlePacketAsync<ExtendQueueFromEntryCountRequest>
                    (context, packet, req => service.ExtendQueueFromEntryCountAsync
                         (req.ChoreId, req.EntryCount, token), token);
            case OpCode.SwapQueueItems:
                return await HandlePacketAsync<SwapQueueItemsRequest>
                    (context, packet, req => service.SwapQueueItemsAsync
                         (req.ChoreId, context.CurrentUser.Id, req.QueueItemAId, req.QueueItemBId, token), token);
            case OpCode.SwapQueueMembers:
                return await HandlePacketAsync<SwapQueueMembersRequest>
                    (context, packet, req => service.SwapMembersInQueueAsync
                         (context.CurrentUser.Id, req.ChoreId, req.UserAId, req.UserBId, token), token);
            case OpCode.InsertQueueItem:
                return await HandlePacketAsync<InsertQueueItemRequest>
                    (context, packet, req => service.InsertQueueEntryAsync
                         (req.ChoreId, context.CurrentUser.Id, req.Entry, token), token);
            case OpCode.InsertMemberInQueue:
                return await HandlePacketAsync<InsertMemberInQueueRequest>
                    (context, packet, req => service.InsertMemberInQueueAsync
                         (req.ChoreId, context.CurrentUser.Id, req.MemberId, req.DesiredOrderRotationIdx, token), token);
            case OpCode.DeleteQueueItem:
                return await HandlePacketAsync<DeleteQueueItemRequest>
                    (context, packet, req => service.DeleteQueueEntryAsync
                         (req.ChoreId, context.CurrentUser.Id, req.Entry, token), token);
            case OpCode.DeleteQueueMember:
                return await HandlePacketAsync<DeleteMemberFromQueueRequest>
                    (context, packet, req => service.DeleteMemberFromQueueAsync
                         (req.ChoreId, context.CurrentUser.Id, req.MemberId, token), token);
            case OpCode.RegenerateQueue:
                return await HandlePacketAsync<RegenerateQueueRequest>
                    (context, packet, req => service.RegenerateQueueAsync
                         (req.ChoreId, context.CurrentUser.Id, token), token);
            case OpCode.ChangeQueueItemInterval:
                return await HandlePacketAsync<ChangeItemIntervalRequest>
                    (context, packet, req => service.ChangeQueueEntryIntervalAsync
                         (req.ChoreId, context.CurrentUser.Id, req.QueueEntryId, req.Interval, token), token);
            case OpCode.CompleteCurrentQueue:
                return await HandlePacketAsync<CompleteCurrentItemRequest>
                    (context, packet, req => service.CompleteCurrentQueueEntryAsync
                         (context.CurrentUser.Id, req.ChoreId, token), token);
            default:
                return false;
        }
    }

    public override OpCode[] GetHandledCodes()
    {
        return [
            OpCode.ExtendQueueFromDays,
            OpCode.ExtendQueueFromEntryCount,
            OpCode.SwapQueueItems,
            OpCode.SwapQueueMembers,
            OpCode.InsertQueueItem,
            OpCode.InsertMemberInQueue,
            OpCode.DeleteQueueItem,
            OpCode.DeleteQueueMember,
            OpCode.RegenerateQueue,
            OpCode.ChangeQueueItemInterval,
            OpCode.CompleteCurrentQueue
        ];
    }
}
