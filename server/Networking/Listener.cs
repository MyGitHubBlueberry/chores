using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Shared;

namespace Server.Networking;

public class Listener {
    IPEndPoint endPoint;
    public Listener(int port) {
        if (!IsPortAvailable(port)) 
            throw new ArgumentException();
        endPoint = ConfigureEndPoint(port);
    }

    IPEndPoint ConfigureEndPoint(int port) {
        IPHostEntry localhost = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress localIpAddress = localhost.AddressList[0];
        return new(localIpAddress, port);
    }

    public async void Listen() {
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

    async Task HandleClientAsync(Socket client) {
        int bytesRead;
        byte[] responceBytes = new byte[RemoveMe.BUFF_SIZE];
        char[] responceChars = new char[RemoveMe.BUFF_SIZE];
        while ((bytesRead = await client.ReceiveAsync(responceBytes)) != 0)
        {
            string recieved = Encoding.UTF8.GetString(responceBytes, 0, bytesRead); 
            Console.WriteLine(recieved);
            // builder.Append(responceChars, 0, charCount);
            await client.SendAsync(Encoding.UTF8.GetBytes("Recieved"));
        }
        Console.WriteLine("Server is not longer listening to a client");
    }

    public static bool IsPortAvailable(int port) {
        if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort) 
            return false;
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
