using Shared.Database.Models;

namespace Shared.Networking.Packets;

public record ExtendQueueFromDaysRequest(
    int ChoreId,
    int Days
) : Request;

public record ExtendQueueFromEntryCountRequest(
    int ChoreId,
    int EntryCount
) : Request;

public record SwapQueueItemsRequest(
    int ChoreId,
    int QueueItemAId,
    int QueueItemBId
) : Request;

public record SwapQueueMembersRequest(
    int ChoreId,
    int UserAId,
    int UserBId
) : Request;

public record InsertQueueItemRequest(
    int ChoreId,
    ChoreQueue Entry
) : Request;

public record InsertMemberInQueueRequest(
    int ChoreId,
    int MemberId,
    int DesiredOrderRotationIdx
) : Request;

public record DeleteQueueItemRequest(
    int ChoreId,
    ChoreQueue Entry
) : Request;

public record DeleteMemberFromQueueRequest(
    int ChoreId,
    int MemberId
) : Request;

public record RegenerateQueueRequest(
    int ChoreId
) : Request;

public record ChangeItemIntervalRequest(
    int ChoreId,
    int QueueEntryId,
    TimeSpan Interval
) : Request;

public record CompleteCurrentItemRequest(
    int ChoreId,
    int QueueEntryId,
    TimeSpan Interval
) : Request;
