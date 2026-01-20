namespace Shared.Networking.Packets;

public record CreateChoreRequest(
    string Title,
    string? Body = null,
    string? AvatarUrl = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    TimeSpan? Duration = null,
    TimeSpan? Interval = null
);

public record UpdateChoreDetailsRequest(
    int ChoreId,
    string? Title = null,
    string? Body = null,
    string? AvatarUrl = null
);

public record UpdateChoreScheduleRequest(
    int ChoreId,
    DateTime? EndDate = null,
    TimeSpan? Duration = null,
    TimeSpan? Interval = null
);

public record AddMembersRequest(
    int ChoreId,
    Dictionary<string, MemberStatus> UsernamesToMemberStatuses
);

public record MemberStatus(
    bool IsAdmin = false,
    int? RotationOrder = null
);
