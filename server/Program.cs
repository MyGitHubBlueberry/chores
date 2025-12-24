using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Server.Args;

namespace Server;

class Program
{
    static async Task Main(string[] args)
    {
        int port; 
        if (!ArgParser.Parse(args, out port)) 
            return;
        IPEndPoint endPoint = await ConfigureEndPoint(port);

        using Socket listener = new(
            endPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);

        listener.Bind(endPoint);
        listener.Listen();

        // List<Task> clients = new List<Task>();

        while (true) {
            var client = await listener.AcceptAsync();
            Console.WriteLine("New client connected");
            _ = HandleClientAsync(client);
            // clients.Add(Task.Run(() => HandleClientAsync(handler)));
        }
        // Task.WaitAll(clients);
    }

    static async Task HandleClientAsync(Socket client) {
        int bytesRead;
        byte[] responceBytes = new byte[Shared.Shared.BUFF_SIZE];
        char[] responceChars = new char[Shared.Shared.BUFF_SIZE];
        while ((bytesRead = await client.ReceiveAsync(responceBytes)) != 0)
        {
            string recieved = Encoding.UTF8.GetString(responceBytes, 0, bytesRead); 
            Console.WriteLine(recieved);
            // builder.Append(responceChars, 0, charCount);
            await client.SendAsync(Encoding.UTF8.GetBytes("Recieved"));
        }
        Console.WriteLine("Server is not longer listening to a client");
    }

    static async Task<IPEndPoint> ConfigureEndPoint(int port) {
        IPHostEntry localhost = await Dns.GetHostEntryAsync(Dns.GetHostName());
        IPAddress localIpAddress = localhost.AddressList[0];
        return new(localIpAddress, port);
    }
}
