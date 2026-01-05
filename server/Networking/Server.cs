using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shared;
using Shared.Networking;

namespace Networking;

public class Server : IDisposable
{
    readonly IPEndPoint endPoint;
    Socket sock;
    CancellationTokenSource cts;

    public Server(int port)
    {
        if (!IsPortAvailable(port))
            throw new ArgumentException();
        endPoint = ConnectionHelper.ConfigureEndPoint(port);
        sock = new(
                endPoint.AddressFamily,
                SocketType.Stream,
                ProtocolType.Tcp);
        cts = new();
    }

    public async Task ListenAsync()
    {
        if (sock.IsBound) return;

        sock.Bind(endPoint);
        sock.Listen();


        List<Task> clients = new List<Task>();
        while (!cts.IsCancellationRequested)
        {
            Socket client;
            try
            {
                client = await sock.AcceptAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            Console.WriteLine("New client connected");
            clients.Add(HandleClientAsync(client, cts.Token));

            clients.RemoveAll(t => t.IsCompleted);
        }
        Console.WriteLine("Waiting for clients to finish.");
        await Task.WhenAll(clients);
        Console.WriteLine("All clients are finished. Closing server socket.");
        sock.Close();
    }

    async Task HandleClientAsync(Socket client, CancellationToken token)
    {
        using NetworkStream stream = new NetworkStream(client);
        while (!token.IsCancellationRequested)
        {
            if (!await HandleClientPackets
                    (await PacketProtocol.ReadPacket(stream, token), stream))
                break;
        }
        client.Close();
        client.Dispose();
    }

    private async Task<bool> HandleClientPackets(ReadPacket packet, NetworkStream stream)
    {
        switch (packet.code)
        {
            case OpCode.Test:
                Console.WriteLine("Test message");
                Console.WriteLine($"Recieved json: {packet.jsonData}");
                TestJson? data = JsonSerializer.Deserialize<TestJson>(packet.jsonData);
                Console.WriteLine($"Recieved data is: {data?.age}, {data?.name}");
                await PacketProtocol
                    .SendPacketAsync(stream,
                            new SendPacket<TestJson>(
                                OpCode.Test,
                                new TestJson(21, "NOT " + data?.name)));
                break;
            case OpCode.Disconnect:
                Console.WriteLine("Client disconnected");
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

    public void Cancel() => cts?.Cancel();

    public void Dispose()
    {
        cts.Cancel();
        sock?.Dispose();
        cts?.Dispose();
    }
}
