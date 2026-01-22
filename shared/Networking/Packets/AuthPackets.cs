using Shared.Database.Models;

namespace Shared.Networking.Packets;

public record LoginRequest(string Username, string Password);
public record LoginResponce(Result<User> Result);
public record RegisterRequest(string Username, string Password);
public record RegisterResponce(Result<User> Result);
