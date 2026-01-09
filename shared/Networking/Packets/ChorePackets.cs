namespace Shared.Networking.Packets;

public record CreateChoreRequest(
    string Title,
    string? Body = null,
    string? AvatarUrl = null,
    DateTime? StartDate = null,
    TimeSpan? Duration = null,
    TimeSpan? Interval = null
);

public record UpdateChoreDetailsRequest(
    int ChoreId,
    string? Title,
    string? Body,
    string? AvatarUrl
);

public record UpdateChoreScheduleRequest(
    int ChoreId,
    DateTime? StartDate,
    TimeSpan? Duration,
    TimeSpan? Interval
);

public record AddMemberRequest(
    int ChoreId,
    string Username,
    bool IsAdmin = false,
    int? RotationOrder = null
);
