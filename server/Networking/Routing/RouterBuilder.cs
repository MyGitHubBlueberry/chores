using System.Diagnostics;
using Database.Services;
using Networking.Handlers;

namespace Networking.Routing;

public class RouterBuilder
{
    Router router = new();

    public RouterBuilder WithHandler(IPacketHandler handler)
    {
        foreach (var code in handler.GetHandledCodes())
        {
            Debug.Assert(router[code] is null);
            router[code] = handler;
        }

        return this;
    }

    public RouterBuilder WithAuthenticationHandler(UserService service)
    {
        var handler = new AuthHandler(service);
        return this.WithHandler(handler);
    }

    public RouterBuilder WithUserHandler(UserService service)
    {
        var handler = new UserHandler(service);
        return this.WithHandler(handler);
    }

    public RouterBuilder WithChoreHandler(ChoreService service)
    {
        var handler = new ChoreHandler(service);
        return this.WithHandler(handler);
    }

    public RouterBuilder WithChoreMemberHandler(ChoreMemberService service)
    {
        var handler = new ChoreMemberHandler(service);
        return this.WithHandler(handler);
    }

    public RouterBuilder WithChoreQueueHandler(ChoreQueueService service)
    {
        var handler = new ChoreQueueHandler(service);
        return this.WithHandler(handler);
    }

    public RouterBuilder WithAllHandlers
        (ChoreQueueService qServ, UserService uServ, ChoreMemberService mServ, ChoreService cServ)
    {
        this.WithChoreQueueHandler(qServ)
            .WithUserHandler(uServ)
            .WithAuthenticationHandler(uServ)
            .WithChoreMemberHandler(mServ)
            .WithChoreHandler(cServ);
        return this;
    }

    public Router Build()
    {
        Debug.Assert(router.HandlerCount != 0);
        return router;
    }
}
