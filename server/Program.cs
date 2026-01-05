using System;

using System.Threading.Tasks;
using Args;
using Networking;

class Program
{
    static async Task Main(string[] args)
    {
        int port;
        if (!ArgParser.Parse(args, out port))
            return;

        Server server;
        try
        {
            server = new(port);
        }
        catch (ArgumentException)
        {
            Console.WriteLine("Invalid port");
            return;
        }

        using var db = new Database.Context();
        _ = Task.Run(()
                => {
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey();
            }
            while (key.KeyChar != 'q');
            server.Cancel();
        });
        await server.ListenAsync();
    }
}
