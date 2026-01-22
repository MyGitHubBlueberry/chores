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
                        Debug.Assert(request != null);

                        var responce = await service.LoginAsync(request);

                        RegisterResponce data = new(responce);
                        SendPacket<RegisterResponce> sendPacket = new (packet.code, data);
                        await PacketProtocol.SendPacketAsync(stream, sendPacket);
                        return responce.IsSuccess;

                    }
                case OpCode.Register:
                    {
                        var request = JsonSerializer.Deserialize<RegisterRequest>(packet.jsonData);
                        Debug.Assert(request != null);

                        var responce = await service.RegisterAsync(request);

                        RegisterResponce data = new(responce);
                        SendPacket<RegisterResponce> sendPacket = new (packet.code, data);
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
