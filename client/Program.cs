using System;
using System.Text.Json;
using System.Threading.Tasks;

using Networking;
using Shared;
using Shared.Networking;

class Program
{
    static async Task Main(string[] args)
    {
        using var client = new Client();
        await client.ConnectAsync();

        while (true)
        {
            Console.Write("Type message: ");
            string input = Console.ReadLine()?.Trim() ?? "";
            if (input == "q")
            {
                client.Disconnect();
                break;
            }

            TestJson data = new TestJson(age: 20, name: input);
            SendPacket<TestJson> packet = new (OpCode.Test, data);

            await client.SendAsync(packet);
        }
    }
}
