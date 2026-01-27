using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class AuthHandler(UserService service) : PacketHandler
{
    protected override async Task<bool> HandleCodesAsync
        (ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        switch (packet.code)
        {
            case OpCode.Login:
                return await HandlePacketAsync<LoginRequest, User>
                    (context, packet, async req => {
                         var result = await service.LoginAsync(req, token);
                         if (result.IsSuccess)
                            context.CurrentUser = result.Value;
                         return result;
                     }, token);
            case OpCode.Register:
                return await HandlePacketAsync<RegisterRequest, User>
                    (context, packet, req => service.RegisterAsync(req, token), token);
            default:
                return false;
        }
    }

    public override OpCode[] GetHandledCodes()
    {
        return [OpCode.Login, OpCode.Register];
    }
}
