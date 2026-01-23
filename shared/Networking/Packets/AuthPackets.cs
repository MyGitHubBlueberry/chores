using Shared.Database.Models;

namespace Shared.Networking.Packets;

public abstract record Request;

public record LoginRequest(string Username, string Password) : Request;
public record LoginResponce(Result<User> Result);
public record RegisterRequest(string Username, string Password): Request;
public record RegisterResponce(Result<User> Result);
