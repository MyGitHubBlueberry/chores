using System;

using System.Threading.Tasks;
using Args;
using Networking.Routing;
using Networking;
using Database.Services;
using System.Threading;
using Networking.Handlers;
using Shared.Networking;

class Program
{
    static async Task Main(string[] args)
    {
        int port;
        if (!ArgParser.Parse(args, out port))
            return;

        using var cts = new CancellationTokenSource();
        using var db = new Database.Context();

        var userService = new UserService(db, cts.Token);
        var permissionService = new ChorePermissionService(db);
        var queueService = new ChoreQueueService(db, permissionService);
        var choreService = new ChoreService(db, queueService, permissionService);
        var memberService = new ChoreMemberService(db, queueService, choreService, permissionService);

        var router = new RouterBuilder()
            .WithAllHandlers(queueService, userService, memberService, choreService)
            .WithHandler(new DebugHandler())
            .Build();

        using Server server = new(port, router, cts.Token);

        _ = Task.Run(()
                => {
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey();
            }
            while (key.KeyChar != 'q');
            cts.Cancel();
        });

        await server.ListenAsync();
    }
}
