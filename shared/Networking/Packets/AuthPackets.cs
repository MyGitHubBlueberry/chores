using Shared.Database.Models;

namespace Shared.Networking.Packets;

public record LoginRequest(string username, string passwordHash);
public record LoginResponce(Result<User> result);
public record RegisterRequest(string username, string passwordHash);
public record RegisterResponce(Result<User> result);
