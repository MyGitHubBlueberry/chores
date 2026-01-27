using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Networking;
using Shared.Networking;
using Shared.Networking.Packets;

public abstract class PacketHandler
{
    public abstract OpCode[] GetHandledCodes();

    public async Task<bool> HandleAsync
        (ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            return await HandleCodesAsync();
        }
        return false;
    }

    protected abstract Task<bool> HandleCodesAsync(ClientContext context, ReadPacket packet, CancellationToken token = default);

    protected async Task<bool> HandlePacketAsync<Req, Res>
        (ClientContext context, ReadPacket packet, Func<Req, Task<Result<Res>>> func, CancellationToken token)
    where Req : Request
    {
        return await HandlePacketAsync(context, packet, func, token);
    }

    protected async Task<bool> HandlePacketAsync<Req>
        (ClientContext context, ReadPacket packet, Func<Req, Task<Result>> func, CancellationToken token)
        where Req : Request
    {
        Req? request;
        try
        {
            request = JsonSerializer.Deserialize<Req>(packet.jsonData);
        }
        catch { return false; }
        if (request is null) return false;

        SendPacket<Result> sendPacket;
        var result = await func.Invoke(request);
        sendPacket = new(packet.code, result);
        await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
        return true;
    }
}
