namespace Networking;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shared;
using Shared.Networking;
using Shared.Networking.Packets.Debug;

public class Client : IDisposable
{
    public Action<ReadPacket> PacketRecieved;
    public bool IsConnected { get => sock?.Connected ?? false; }
    public CancellationToken Token { get => cts.Token; }

    
    readonly CancellationTokenSource cts;
    NetworkStream? stream;
    Socket sock;

    public Client()
    {
        sock = new(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp);
        cts = new();
    }

    public async Task ConnectAsync(string ip, int port, int tryCount)
    {
        if (sock.Connected) return;
        
        if (!IPAddress.TryParse(ip, out IPAddress? address))
        {
            throw new ArgumentException("Invalid IP Address");
        }
        IPEndPoint endPoint = new IPEndPoint(address, port);
        
        if (sock is null) 
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        
        while (!cts.IsCancellationRequested && !sock.Connected && tryCount-- > 0)
        {
            try
            {
                await sock.ConnectAsync(endPoint, cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
                Console.WriteLine($"Reconnecting...");
                await Task.Delay(1000, cts.Token);
            }
        }

        if (!cts.IsCancellationRequested && sock.Connected)
        {
            stream = new NetworkStream(sock);
            _ = RecieveLoopAsync();
            Console.WriteLine("In recieve loop");
        }
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
            Console.WriteLine("waiting to read packet");
            if (!HandleResponces(await PacketProtocol.ReadPacket(stream)))
                break;
        }
        Disconnect();
    }

    bool HandleResponces(ReadPacket packet)
    {
        Console.WriteLine("invoked packet recieved");
        PacketRecieved.Invoke(packet);
        switch (packet.code)
        {
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
