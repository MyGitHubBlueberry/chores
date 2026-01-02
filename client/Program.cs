using System;
using System.Threading.Tasks;

using Networking;

class Program
{
    static async Task Main(string[] args)
    {
        var client = new Client();
        client.ConnectAsync();

        while (true)
        {
            //get data
            Console.Write("Type message: ");
            string input = Console.ReadLine()?.Trim() ?? "";
            if (input == "q")
            {
                client.Disconnect();
                break;
            }
            //send
            client.SendAsync(input);
            //recieve
            string recieved = await client.RecieveAsync();
            if (recieved == String.Empty) {
                client.Disconnect();
                break;
            }
            Console.WriteLine(recieved);
        }
    }
}
