using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class AuthHandler(UserService service) : IPacketHandler
{
    public async Task<bool> HandleAsync
        (NetworkStream stream, ReadPacket packet, CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            switch (packet.code)
            {
                case OpCode.Login:
                    {
                        var request = JsonSerializer.Deserialize<LoginRequest>(packet.jsonData);
                        // service.
                        return true;
                    }
                case OpCode.Register:
                    {
                        var request = JsonSerializer.Deserialize<RegisterRequest>(packet.jsonData);
                        Debug.Assert(request != null);
                        var responce = await service.CreateUserAsync(request);
                        RegisterResponce data = new(responce);
                        SendPacket<RegisterResponce> sendPacket = new (OpCode.Register, data);
                        await PacketProtocol.SendPacketAsync(stream, sendPacket);
                        return responce.IsSuccess;
                    }
                default:
                    return false;
            }
        }
        return false;
    }
}
