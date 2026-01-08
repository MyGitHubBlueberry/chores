namespace Shared.Networking.Packets;

public record UpdateChoreDetailsRequest(
    int ChoreId,
    string Title, 
    string? Body, 
    string? AvatarUrl
);

//todo: what make nullable
public record UpdateChoreScheduleRequest(
    int ChoreId,
    DateTime StartDate,
    TimeSpan Duration,
    TimeSpan Interval
);
