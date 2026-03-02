using System;
using System.Text.Json;
using System.Threading.Tasks;
using client.Views;
using Networking;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public class CreateChoreModel
{
    private readonly Client client;
    private Action<Result<Chore>>? createChoreCallback;

    public CreateChoreModel(Client client)
    {
        this.client = client;
        client.PacketReceived += HandlePackets;
    }

    private void HandlePackets(ReadPacket packet)
    {
        Console.WriteLine("create chore received packet. Packet code is: " + packet.code);
        switch (packet.code)
        {
            case OpCode.CreateChore:
                var result = JsonSerializer.Deserialize<Result<Chore>>(packet.jsonData);
                Console.WriteLine("about to invoke callback");
                createChoreCallback?.Invoke(result);
                Console.WriteLine("callback invoked");
                break;
        }
    }

    public async Task CreateChoreAsync(CreateChoreRequest request, Action<Result<Chore>> callback)
    {
        createChoreCallback = callback;
        await client.SendAsync(new SendPacket<CreateChoreRequest>(OpCode.CreateChore, request));
    }
}