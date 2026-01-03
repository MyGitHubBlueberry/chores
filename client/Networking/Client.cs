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
    readonly IPEndPoint endPoint;
    readonly CancellationTokenSource cts;
    NetworkStream stream;
    Socket sock;

    public Client()
    {
        endPoint = ConnectionHelper.ConfigureEndPoint(7777);
        sock = new(
            endPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);
        cts = new();
        // cts.Token.Register(sock.Close);
    }

    public async Task ConnectAsync()
    {
        if (sock.Connected) return;
        try
        {
            await sock.ConnectAsync(endPoint, cts.Token);
            stream = new NetworkStream(sock);
            _ = RecieveLoopAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
        }
    }

    public async Task SendAsync<T>(SendPacket<T> packet)
    {
        if (!sock.Connected || stream is null) return;

        try
        {
            await PacketProtocol.SendPacketAsync(stream, packet);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Send failed: {ex.Message}");
            Disconnect();
        }
    }

    public async Task RecieveLoopAsync()
    {
        if (stream is null) return;

        try
        {
            while (!cts.Token.IsCancellationRequested && sock.Connected)
            {
                if (!HandleResponces(await PacketProtocol.ReadPacket(stream))) 
                    break;
            }
        }
        catch (Exception ex) 
        {
            Console.WriteLine($"Exception recieving a message: {ex.Message}");
        }
        finally
        {
            Disconnect();
        }
    }

    bool HandleResponces(ReadPacket packet)
    {
        switch (packet.code)
        {
            case OpCode.Test:
                TestJson data = JsonSerializer.Deserialize<TestJson>(packet.jsonData);
                Console.WriteLine($"Recieved data is: {data?.age}, {data?.name}");
                break;
            case OpCode.Error:
                Console.WriteLine("Disconnect with error");
                return false;
            case OpCode.Disconnect:
                Console.WriteLine("Disconnect with read after end of stream");
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
            } catch {}
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
