namespace Shared.Networking.Packets;

public record GetUserByIdRequest(int UserId) : Request;
public record GetUserByNameRequest(string Username) : Request;
public record DeleteUserRequest() : Request;
public record GetOwnedChoresByIdRequest(int UserId) : Request;
public record GetMembershipsByIdRequest(int UserId) : Request;
public record GetLogsByUserIdRequest(int UserId) : Request;
