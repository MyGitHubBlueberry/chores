using System;

using System.Threading.Tasks;
using Args;
using Networking.Routing;
using Networking;
using Database.Services;
using System.Threading;
using Database;
using Microsoft.EntityFrameworkCore;
using Networking.Handlers;
using Shared.Networking;
using Microsoft.Extensions.DependencyInjection;

class Program
{
    static async Task Main(string[] args)
    {
        int port;
        if (!ArgParser.Parse(args, out port))
            return;
        
        var serviceCollection = new ServiceCollection();
        
        serviceCollection.AddDbContext<Context>();
        
        serviceCollection.AddScoped<UserService>();
        serviceCollection.AddScoped<ChorePermissionService>();
        serviceCollection.AddScoped<ChoreQueueService>();
        serviceCollection.AddScoped<ChoreService>();
        serviceCollection.AddScoped<ChoreMemberService>();
        
        serviceCollection.AddSingleton<AuthHandler>();
        serviceCollection.AddSingleton<UserHandler>();
        serviceCollection.AddSingleton<ChoreHandler>();
        serviceCollection.AddSingleton<ChoreMemberHandler>();
        serviceCollection.AddSingleton<ChoreQueueHandler>();
        serviceCollection.AddSingleton<DebugHandler>();
        
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var cts = new CancellationTokenSource();

        var router = new RouterBuilder(serviceProvider)
            .WithAllHandlers()
            .Build();

        using Server server = new(port, router, cts.Token);

        _ = Task.Run(() => {
            while (Console.ReadKey().KeyChar != 'q');
            cts.Cancel();
        }, cts.Token);
        
        Console.WriteLine($"Server started on port {port}");
        await server.ListenAsync();
    }
}
