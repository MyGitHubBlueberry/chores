using System;

namespace Server;

using System.Threading.Tasks;
using Server.Args;
using Server.Networking;


class Program
{
    static async Task Main(string[] args)
    {
        int port; 
        if (!ArgParser.Parse(args, out port))
            return;

        Listener listener;
        try {
            listener = new Listener(port);
        } catch (ArgumentException) {
            Console.WriteLine("Invalid port");
            return;
        }

        using var db = new Database.Context();
        await listener.ListenAsync();
    }
}
