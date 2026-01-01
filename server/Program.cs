using System;
using System.Threading.Tasks;

namespace Server;

using Server.Args;
using Server.Networking;


class Program
{
    static async Task Main(string[] args)
    {
        int port; 
        if (!ArgParser.Parse(args, out port)) 
            return;

        try {
            Listener listener = new Listener(port);
        } catch (ArgumentException) {
            Console.WriteLine("Invalid port");
            return;
        }

        using var db = new Database.Context();
    }
}
