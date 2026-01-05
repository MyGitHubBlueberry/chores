using System;
using System.Threading.Tasks;

using Networking;
using Shared.Networking;
using Shared.Networking.Packets.Debug;

class Program
{
    static async Task Main(string[] args)
    {
        using var client = new Client();
        await client.ConnectAsync();

        while (client.IsConnected)
        {
            Console.Write("Type message: ");
            string input = Console.ReadLine()?.Trim() ?? "";
            if (input == "q")
            {
                client.Disconnect();
                break;
            }

            TestRequest data = new(input);
            SendPacket<TestRequest> packet = new (OpCode.Test, data);

            await client.SendAsync(packet);
        }
    }
}
