namespace Shared.Networking.Packets;

public abstract record Request;

public record LoginRequest(string Username, string Password) : Request;
public record RegisterRequest(string Username, string Password): Request;
