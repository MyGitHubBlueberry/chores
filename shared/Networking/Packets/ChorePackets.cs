namespace Shared.Networking.Packets;

public record CreateChoreRequest(
    string Title,
    string? Body,
    string? AvatarUrl,
    DateTime? StartDate,
    TimeSpan? Duration,
    TimeSpan? Interval
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
