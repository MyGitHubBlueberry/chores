namespace Networking;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shared;
using Shared.Networking;

public class Client
{
    readonly Socket sock;
    readonly IPEndPoint endPoint;
    readonly CancellationTokenSource cancellationSource;

    public Client()
    {
        endPoint = ConnectionHelper.ConfigureEndPoint(7777);
        sock = new(
            endPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);
        cancellationSource = new();
    }

    public async void ConnectAsync()
    {
        await sock.ConnectAsync(endPoint, cancellationSource.Token);
    }

    public async void SendAsync(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        int bytesSent = 0;
        while (bytesSent < data.Length)
        {
            // as memory creates readonly view into array and bytesSent is starting offset
            bytesSent += await sock.SendAsync(data.AsMemory(bytesSent));
            Console.WriteLine($"Sending data: sent {bytesSent}; data {data.Length};");
        }
    }

    public async Task<string> RecieveAsync()
    {
        byte[] responceBytes = new byte[RemoveMe.BUFF_SIZE];
        char[] responceChars = new char[RemoveMe.BUFF_SIZE];
        int bytesRecieved;
        bytesRecieved = await sock.ReceiveAsync(responceBytes, cancellationSource.Token);
        if (bytesRecieved == 0) 
            return String.Empty;
        return Encoding.UTF8.GetString(responceBytes, 0, bytesRecieved);
    }

    public void Disconnect()
    {
        cancellationSource.Cancel();
        sock.Shutdown(SocketShutdown.Both);
    }
}
