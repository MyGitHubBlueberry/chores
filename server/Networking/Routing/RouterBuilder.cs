using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Database.Services;
using Networking.Handlers;

namespace Networking.Routing;

public class RouterBuilder(IServiceProvider services)
{
    Router router = new();

    public RouterBuilder WithHandler<T>() where T : PacketHandler
    {
        var handler = services.GetRequiredService<T>();
        
        foreach (var code in handler.GetHandledCodes())
        {
            Debug.Assert(router[code] is null);
            router[code] = handler;
        }

        return this;
    }
    
    public RouterBuilder WithAllHandlers()
    {
        return this
            .WithHandler<ChoreQueueHandler>()
            .WithHandler<UserHandler>()
            .WithHandler<AuthHandler>()
            .WithHandler<ChoreMemberHandler>()
            .WithHandler<ChoreHandler>()
            .WithHandler<DebugHandler>();
    }

    public Router Build()
    {
        Debug.Assert(router.HandlerCount != 0);
        return router;
    }
}