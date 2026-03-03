using System;
using System.Text.Json;
using System.Threading.Tasks;
using Networking;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public class ChoreManagementModel
{
    private readonly Client client;
    private readonly int choreId;
    private Action<Result> deleteCallback;
    
    public ChoreManagementModel(Client client, ChoreDto chore)
    {
        this.client = client;
        this.choreId = chore.ChoreId;
        this.client.PacketReceived += HandlePackets;
    }

    private void HandlePackets(ReadPacket packet)
    {
        switch (packet.code)
        {
            case OpCode.DeleteChore:
                deleteCallback?.Invoke(JsonSerializer.Deserialize<Result>(packet.jsonData));
                break;
        }
    }

    public async Task RequestDeleteChoreAsync(Action<Result> deleteCallback)
    {
        if (!client.IsConnected) return;

        this.deleteCallback = deleteCallback;
        await client.SendAsync(new SendPacket<DeleteChoreRequest>(OpCode.DeleteChore, new DeleteChoreRequest(choreId)));
    }
}