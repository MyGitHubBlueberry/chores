namespace Shared.Networking.Packets;

public record AddMembersRequest(
    int ChoreId,
    Dictionary<string, MemberStatus> UsernamesToMemberStatuses
) : Request;

public record MemberStatus(
    bool IsAdmin = false,
    int? RotationOrder = null
) : Request;

public record DeleteMemberRequest(
    int ChoreId,
    int UserId
) : Request;

public record SetAdminStatusRequest(
    int ChoreId,
    int UserId,
    bool IsAdmin
) : Request;
