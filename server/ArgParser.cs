using System.Net;
using System.Net.Sockets;
using System;

namespace Server.Args;

public static class ArgParser {
    public static bool Parse(string[] args, out int port) {
        if (args.Length == 1) {
            if (!int.TryParse(args[0], out port) && IsPortAvailable(port)) {
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

    static bool IsPortAvailable(int port) {
        TcpListener l = new TcpListener(IPAddress.Loopback, port);
        try {
            l.Start();
        } catch {
            return false;
        }
        l.Stop();
        return true;
    }
}
