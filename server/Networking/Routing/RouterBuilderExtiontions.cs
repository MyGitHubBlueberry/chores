using Database.Services;
using Networking.Handlers;
using Shared.Networking;

namespace Networking.Routing.Extensions;
public static class RouterBuilderExtensions
{
    public static RouterBuilder WithAuthenticationHandler(this RouterBuilder builder, UserService service)
    {
        var handler = new AuthHandler(service);
        return builder.WithHandler(handler, OpCode.Login, OpCode.Register);
    }
}
