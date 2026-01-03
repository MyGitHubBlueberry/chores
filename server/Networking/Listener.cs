using System;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Shared;
using Shared.Networking;

namespace Server.Networking;

public class Listener : IDisposable
{
    readonly IPEndPoint endPoint;
    Socket sock;

    public Listener(int port)
    {
        if (!IsPortAvailable(port))
            throw new ArgumentException();
        endPoint = ConnectionHelper.ConfigureEndPoint(port);
        sock = new(
                endPoint.AddressFamily,
                SocketType.Stream,
                ProtocolType.Tcp);
    }

    public async Task ListenAsync()
    {
        sock.Bind(endPoint);
        sock.Listen();

        // List<Task> clients = new List<Task>();
        while (true)
        {
            var client = await sock.AcceptAsync();
            Console.WriteLine("New client connected");
            _ = HandleClientAsync(client);
            // clients.Add(Task.Run(() => HandleClientAsync(handler)));
        }
        // Task.WaitAll(clients);
    }

    async Task HandleClientAsync(Socket client)
    {
        using NetworkStream stream = new NetworkStream(client, ownsSocket: true);
        while
            (await HandleClientPackets
                (await PacketProtocol.ReadPacket(stream), stream)
            ) { }
    }

    private async Task<bool> HandleClientPackets(ReadPacket packet, NetworkStream stream)
    {
        switch (packet.code)
        {
            case OpCode.Test:
                Console.WriteLine("Test message");
                Console.WriteLine($"Recieved json: {packet.jsonData}");
                TestJson data;
                data = JsonSerializer.Deserialize<TestJson>(packet.jsonData);
                Console.WriteLine($"Recieved data is: {data?.age}, {data?.name}");
                await PacketProtocol.SendPacketAsync(stream, new SendPacket<TestJson>(OpCode.Test, new TestJson(21, "NOT MAX")));
                break;
            case OpCode.Error:
                Console.WriteLine("disconnected with error");
                return false;
            case OpCode.Disconnect:
                Console.WriteLine("disconnected with read after end of stream");
                return false;
        }
        return true;
    }

    public static bool IsPortAvailable(int port)
    {
        if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            return false;
        TcpListener l = new TcpListener(IPAddress.Loopback, port);
        try
        {
            l.Start();
        }
        catch
        {
            return false;
        }
        l.Stop();
        return true;
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
