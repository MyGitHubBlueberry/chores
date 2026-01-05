namespace Networking;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shared;
using Shared.Networking;

public class Client : IDisposable
{
    public bool IsConnected { get => sock?.Connected ?? false; }
    public CancellationToken Token { get => cts.Token; }

    readonly IPEndPoint endPoint;
    readonly CancellationTokenSource cts;
    NetworkStream? stream;
    Socket sock;

    public Client()
    {
        endPoint = ConnectionHelper.ConfigureEndPoint(7777);
        sock = new(
            endPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);
        cts = new();
    }

    public async Task ConnectAsync()
    {
        if (sock.Connected) return;

        while (!cts.IsCancellationRequested && !sock.Connected)
        {
            try
            {
                await sock.ConnectAsync(endPoint, cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
                Console.WriteLine($"Reconnecting...");
                Thread.Sleep(1_000);
            }
        }
        stream = new NetworkStream(sock);
        _ = RecieveLoopAsync();
    }

    public async Task SendAsync<T>(SendPacket<T> packet)
    {
        if (!sock.Connected || stream is null) return;
        await PacketProtocol.SendPacketAsync(stream, packet);
    }

    public async Task RecieveLoopAsync()
    {
        if (stream is null) return;

        while (!cts.Token.IsCancellationRequested && sock.Connected)
        {
            if (!HandleResponces(await PacketProtocol.ReadPacket(stream)))
                break;
        }
        Disconnect();
    }

    bool HandleResponces(ReadPacket packet)
    {
        switch (packet.code)
        {
            case OpCode.Test:
                TestJson? data = JsonSerializer.Deserialize<TestJson>(packet.jsonData);
                Console.WriteLine($"Recieved data is: {data?.age}, {data?.name}");
                break;
            case OpCode.Disconnect:
                Console.WriteLine("Disconnected");
                return false;
            default:
                Console.WriteLine("Not implemented");
                break;
        }
        return true;
    }

    public void Disconnect()
    {
        if (cts.IsCancellationRequested) return;

        cts.Cancel();

        if (sock.Connected)
        {
            try
            {
                sock.Shutdown(SocketShutdown.Both);
            }
            catch { }
        }
        sock.Close();
    }

    public void Dispose()
    {
        Disconnect();
        cts.Dispose();
        stream?.Dispose();
        sock.Dispose();
        GC.SuppressFinalize(this);
    }
}
