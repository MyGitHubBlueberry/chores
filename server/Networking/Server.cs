using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Networking.Routing;
using Shared;
using Shared.Networking;


namespace Networking;

public class Server : IDisposable
{
    readonly IPEndPoint endPoint;
    Socket sock;
    CancellationToken token;
    Router router;

    public Server(int port, Router router, CancellationToken token)
    {
        if (!IsPortAvailable(port))
            throw new ArgumentException();
        endPoint = ConnectionHelper.ConfigureEndPoint(port);
        sock = new(
                endPoint.AddressFamily,
                SocketType.Stream,
                ProtocolType.Tcp);
        this.token = token;
        this.router = router;
    }

    public async Task ListenAsync()
    {
        if (sock.IsBound) return;

        sock.Bind(endPoint);
        sock.Listen();


        List<Task> clients = new List<Task>();
        while (!token.IsCancellationRequested)
        {
            Socket client;
            try
            {
                client = await sock.AcceptAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            Console.WriteLine("New client connected");
            clients.Add(HandleClientAsync(client, token));

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
        if (packet.code == OpCode.Disconnect) {
            Console.WriteLine("Client disconnected");
            return false;
        }
        var handler = router[packet.code];
        if (handler is null) {
            Console.WriteLine("Unknown request");
            return false;
        }
        return await handler.Handle(stream, packet, token);
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

    public void Dispose() => sock?.Dispose();
}
