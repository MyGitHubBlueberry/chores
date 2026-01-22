using System;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shared.Networking;
using Shared.Networking.Packets.Debug;

namespace Networking.Handlers;

public class DebugHandler() : IPacketHandler
{
    public async Task<bool> HandleAsync(NetworkStream stream, ReadPacket packet, CancellationToken token = default)
    {
        while (!token.IsCancellationRequested) {
            switch (packet.code) {
                case OpCode.Test:
                    TestRequest? data = JsonSerializer.Deserialize<TestRequest>(packet.jsonData);
                    if (data is null) return false;
                    Console.WriteLine("Handled test request.");
                    Console.WriteLine("Recieve json: " + packet.jsonData);
                    Console.WriteLine($"Recieved: {data.str}");
                    var test = new TestResponse("~" + data.str + "~");
                    SendPacket<TestResponse> responce = new(OpCode.Test, test);
                    await PacketProtocol.SendPacketAsync(stream, responce);
                    return true;
                default:
                    Console.WriteLine("Couldn't handle");
                    return false;
            }
        }
        return false;
    }
}
