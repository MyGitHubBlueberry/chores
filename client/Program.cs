using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client;

class Program
{
    static async Task Main(string[] args)
    {
        //configure
        IPEndPoint endPoint = await ConfigureEndPoint(7777);
        using Socket sock = new(
            endPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);
        using CancellationTokenSource source = new();
        CancellationToken token = source.Token;

        //connect
        await sock.ConnectAsync(endPoint, token);

        while (true)
        {
            //get data
            Console.Write("Type message: ");
            string input = Console.ReadLine()?.Trim() ?? "";
            if (input == "q")
            {
                source.Cancel();
                break;
            }

            //send
            byte[] data = Encoding.UTF8.GetBytes(input);
            int bytesSent = 0;
            while (bytesSent < data.Length)
            {
                // as memory creates readonly view into array and bytesSent is starting offset
                bytesSent += await sock.SendAsync(data.AsMemory(bytesSent));
                Console.WriteLine($"Sending data: sent {bytesSent}; data {data.Length};");
            }

            //recieve
            byte[] responceBytes = new byte[Shared.Shared.BUFF_SIZE];
            char[] responceChars = new char[Shared.Shared.BUFF_SIZE];
            int bytesRecieved;
            // StringBuilder builder = new();
            // while ((bytesRecieved = await sock.ReceiveAsync(responceBytes, token)) != 0)
            // {
            //     int charCount = Encoding.UTF8.GetChars(responceBytes, 0, bytesRecieved, responceChars, 0); 
            //     builder.Append(responceChars, 0, charCount);
            //     Console.WriteLine("bytesRecieved = " + bytesRecieved);
            //     bytesRecieved = 0;
            // }
            bytesRecieved = await sock.ReceiveAsync(responceBytes, token);
            if (bytesRecieved == 0) break;
            string response = Encoding.UTF8.GetString(responceBytes, 0, bytesRecieved); 
            Console.WriteLine(response);
            // await Console.Out.WriteAsync(builder, token);
        }
        sock.Shutdown(SocketShutdown.Both);
    }

    static async Task<IPEndPoint> ConfigureEndPoint(int port)
    {
        IPHostEntry localhost = await Dns.GetHostEntryAsync(Dns.GetHostName());
        IPAddress localIpAddress = localhost.AddressList[0];
        return new(localIpAddress, port);
    }
}
