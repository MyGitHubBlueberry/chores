using System.Diagnostics;
using Database.Services;
using Networking.Handlers;
using Shared.Networking;

namespace Networking.Routing;

public class RouterBuilder
{
    Router router = new();

    public RouterBuilder WithHandler(IPacketHandler handler, params OpCode[] codes) 
    {
        foreach (var code in codes) {
            Debug.Assert(router[code] is null);
            router[code] = handler;
        }

        return this;
    }

    public RouterBuilder WithAuthenticationHandler(UserService service)
    {
        var handler = new AuthHandler(service);
        return this.WithHandler(handler, OpCode.Login, OpCode.Register);
    }

    public Router Build() 
    {
        Debug.Assert(router.HandlerCount != 0);
        return router;
    }
}
