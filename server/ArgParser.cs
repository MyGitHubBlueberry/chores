using System;

using Server.Networking;
namespace Server.Args;

public static class ArgParser {
    public static bool Parse(string[] args, out int port) {
        if (args.Length == 1) {
            if (!int.TryParse(args[0], out port) && Listener.IsPortAvailable(port)) {
                PrintUseage();
                return false;
            }
            return true;
        }
        port = -1;
        PrintUseage();
        return false;
    }

    static void PrintUseage() {
        Console.WriteLine("USEAGE: ./server <port>");
    }

}
