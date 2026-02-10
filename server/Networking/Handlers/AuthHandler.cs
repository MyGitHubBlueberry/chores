using System;
using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Microsoft.Extensions.DependencyInjection;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class AuthHandler(IServiceScopeFactory scopeFactory) : PacketHandler
{
    protected override async Task<bool> HandleCodesAsync
        (ClientContext context, ReadPacket packet, CancellationToken token = default)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<UserService>();
        
        switch (packet.code)
        {
            case OpCode.Login:
                Console.WriteLine("Recieved login request");
                return await HandlePacketAsync<LoginRequest, User>
                    (context, packet, async req => {
                         var result = await service.LoginAsync(req, token);
                         if (result.IsSuccess)
                            context.CurrentUser = result.Value;
                         return result;
                     }, token);
            case OpCode.Register:
                Console.WriteLine("Recieved reg request");
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
