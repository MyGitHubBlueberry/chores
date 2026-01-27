using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shared.Networking;
using Shared.Networking.Packets.Debug;

namespace Networking.Handlers;

public class DebugHandler() : PacketHandler
{
    protected override async Task<bool> HandleCodesAsync
        (ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        switch (packet.code)
        {
            case OpCode.Test:
                TestRequest? data = JsonSerializer.Deserialize<TestRequest>(packet.jsonData);
                if (data is null) return false;
                Console.WriteLine("Handled test request.");
                Console.WriteLine("Recieve json: " + packet.jsonData);
                Console.WriteLine($"Recieved: {data.str}");
                var test = new TestResponse("~" + data.str + "~");
                SendPacket<TestResponse> responce = new(OpCode.Test, test);
                await PacketProtocol.SendPacketAsync(context.Stream, responce);
                return true;
            default:
                Console.WriteLine("Couldn't handle");
                return false;
        }
    }

    public override OpCode[] GetHandledCodes()
    {
        return [OpCode.Test];
    }
}
