namespace Shared.Networking.Packets;

public record AddMembersRequest(
    int ChoreId,
    Dictionary<string, MemberStatus> UsernamesToMemberStatuses
) : Request;

public record MemberStatus(
    bool IsAdmin = false,
    int? RotationOrder = null
) : Request;

public record DeleteMembersRequest(
    int ChoreId,
    int UserId
) : Request;

public record SetAdminStatusRequest(
    int ChoreId,
    int UserId,
    MemberStatus stasus
) : Request;
