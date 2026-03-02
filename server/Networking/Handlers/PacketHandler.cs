using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public abstract class PacketHandler
{
    public abstract OpCode[] GetHandledCodes();

    public async Task<bool> HandleAsync(
        ClientContext context,
        ReadPacket packet,
        CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            return await HandleCodesAsync(context, packet, token);
        }

        return false;
    }

    protected abstract Task<bool> HandleCodesAsync(
        ClientContext context,
        ReadPacket packet,
        CancellationToken token = default);
    
    protected async Task<bool> HandlePacketAsync<Req, Res>(
        ClientContext context,
        ReadPacket packet,
        Func<Req, Task<Result<Res>>> func,
        CancellationToken token)
        where Req : Request
    {
        Req? request;

        try
        {
            request = JsonSerializer.Deserialize<Req>(packet.jsonData);
        }
        catch
        {
            return false;
        }

        if (request is null)
            return false;

        Console.WriteLine("before func (generic)");

        Result<Res> result;

        try
        {
            result = await func(request);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        Console.WriteLine("after func (generic) with code: " + packet.code);

        var sendPacket = new SendPacket<Result<Res>>(packet.code, result);

        Console.WriteLine("Sent generic packet back");

        await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);

        return true;
    }
    
    protected async Task<bool> HandlePacketAsync<Req>(
        ClientContext context,
        ReadPacket packet,
        Func<Req, Task<Result>> func,
        CancellationToken token)
        where Req : Request
    {
        Req? request;

        try
        {
            request = JsonSerializer.Deserialize<Req>(packet.jsonData);
        }
        catch
        {
            return false;
        }

        if (request is null)
            return false;

        Console.WriteLine("before func (non-generic)");

        Result result;

        try
        {
            result = await func(request);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        Console.WriteLine("after func (non-generic)");

        var sendPacket = new SendPacket<Result>(packet.code, result);

        Console.WriteLine("Sent non-generic packet back");

        await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);

        return true;
    }
}