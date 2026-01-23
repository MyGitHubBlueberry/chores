using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class UserHandler(UserService service) : IPacketHandler
{
    public async Task<bool> HandleAsync
        (ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            switch (packet.code)
            {
                case OpCode.DeleteUser:
                    return await HandleUserDeletionAsync(context, packet, token);
            }
        }
        return false;
    }

    private async Task<bool> HandleUserDeletionAsync
        (ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        var request = JsonSerializer.Deserialize<DeleteUserRequest>(packet.jsonData);
        if (context.CurrentUser is null)
        {
            SendPacket<Result> sendPacket =
                new(packet.code, Result.Fail(ServiceError.Conflict, "Log in first."));
            await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
            return true;
        }
        var result = await service
            .DeleteUserAsync(context.CurrentUser.Id, context.CurrentUser.Id);
        if (result.IsSuccess)
        {
            context.CurrentUser = null;
            SendPacket<Result> sendPacket = new(packet.code, result);
            await PacketProtocol.SendPacketAsync(context.Stream, sendPacket);
        }
        return true;
    }
}
