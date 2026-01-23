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

        var router = new RouterBuilder()
            .WithAuthenticationHandler(userService)
            .WithHandler(new DebugHandler(), OpCode.Test)
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
