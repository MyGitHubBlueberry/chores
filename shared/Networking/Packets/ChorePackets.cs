namespace Shared.Networking.Packets;

public record CreateChoreRequest(
    string Title,
    string? Body = null,
    string? AvatarUrl = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    TimeSpan? Duration = null,
    TimeSpan? Interval = null
) : Request;

public record DeleteChoreRequest(
    int ChoreId
) : Request;

public record PauseChoreRequest(
    int ChoreId
) : Request;

public record UnpauseChoreRequest(
    int ChoreId
) : Request;

public record UpdateChoreDetailsRequest(
    int ChoreId,
    string? Title = null,
    string? Body = null,
    string? AvatarUrl = null
) : Request;

public record UpdateChoreScheduleRequest(
    int ChoreId,
    DateTime? EndDate = null,
    TimeSpan? Duration = null,
    TimeSpan? Interval = null
) : Request;

public record AddMembersRequest(
    int ChoreId,
    Dictionary<string, MemberStatus> UsernamesToMemberStatuses
) : Request;

public record MemberStatus(
    bool IsAdmin = false,
    int? RotationOrder = null
) : Request;
