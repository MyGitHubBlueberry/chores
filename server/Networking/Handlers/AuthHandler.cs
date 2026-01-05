using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Networking;

namespace Networking.Handlers;

public class AuthHandler(UserService service) : IPacketHandler
{
    public async Task<bool> Handle(NetworkStream stream, ReadPacket packet, CancellationToken token = default)
    {
        while (!token.IsCancellationRequested) {
            switch (packet.code) {
                case OpCode.Login:
                    Console.WriteLine("Login");
                    return true;
                case OpCode.Register:
                    Console.WriteLine("Register");
                    return true;
                default:
                    return false;
            }
        }
        return false;
    }
}
