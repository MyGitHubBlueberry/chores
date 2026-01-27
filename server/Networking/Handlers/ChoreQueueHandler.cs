using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class ChoreQueueHandler(ChoreQueueService service) : IPacketHandler
{
    public async Task<bool> HandleAsync
        (ClientContext context, ReadPacket packet, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (context.CurrentUser is null)
                return false;
            switch (packet.code)
            {
                case OpCode.ExtendQueueFromDays:
                case OpCode.ExtendQueueFromEntryCount:
                case OpCode.SwapQueueItems:
                case OpCode.SwapQueueMembers:
                case OpCode.InsertQueueItem:
                case OpCode.InsertMemberInQueue:
                case OpCode.DeleteQueueItem:
                case OpCode.DeleteQueueMember:
                case OpCode.RegenerateQueue:
                case OpCode.RegenerateQueueIntervals:
                case OpCode.CompleteCurrentQueue:
                default:
                    return false;
        }
    }
        return false;
    }

    private async Task<bool> Handle<Req, Res>
    (ClientContext context, ReadPacket packet, Func<Req, Task<Result<Res>>> func, CancellationToken token)
    where Req : Request
    {
        return await Handle(context, packet, func, token);
    }

    private async Task<bool> Handle<Req>
        (ClientContext context, ReadPacket packet, Func<Req, Task<Result>> func, CancellationToken token)
        where Req : Request
    {
        var request = JsonSerializer.Deserialize<Req>(packet.jsonData);
        Debug.Assert(request is not null);
        SendPacket<Result> sendPacket;
        var result = await func.Invoke(request);
        sendPacket = new(packet.code, result);
        await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
        return true;
    }
}
