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
    int queueItemAId,
    int queueItemBId
) : Request;

public record SwapQueueMembersRequest(
    int ChoreId,
    int userAId,
    int userBId
) : Request;

public record InsertQueueItemRequest(
    int ChoreId,
    ChoreQueue entry
) : Request;

public record InsertMemberInQueueRequest(
    int ChoreId,
    int memberId,
    int desiredOrderRotationIdx
) : Request;

public record DeleteQueueItemRequest(
    int ChoreId,
    ChoreQueue entry
) : Request;

public record DeleteMemberFromQueueRequest(
    int ChoreId,
    int memberId
) : Request;

public record RegenerateQueueRequest(
    int ChoreId
) : Request;

public record ChangeItemIntervalRequest(
    int ChoreId,
    int queueEntryId,
    TimeSpan interval
) : Request;

public record CompleteCurrentItemRequest(
    int ChoreId,
    int queueEntryId,
    TimeSpan interval
) : Request;
