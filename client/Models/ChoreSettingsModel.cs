using System;
using System.Text.Json;
using System.Threading.Tasks;
using Networking;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public class ChoreSettingsModel
{
    private readonly Client client;
    public Action<Result> OnNameVerificationResponseReceived;
    public ChoreSettingsModel(Client client)
    {
        this.client = client;
        this.client.PacketReceived += OnPacketReceived;
    }

    private void OnPacketReceived(ReadPacket packet)
    {
        switch (packet.code)
        {
            case OpCode.VerifyChoreName:
                var result = JsonSerializer.Deserialize<Result>(packet.jsonData);
                if (result is null)
                {
                    Console.WriteLine("Server error");
                    break;
                }
                OnNameVerificationResponseReceived.Invoke(result);
                break;
        }
    }

    public async Task IsChoreNameUnique(string name)
    {
        await client.SendAsync(new SendPacket<CheckChoreNameUniquenessRequest>(OpCode.VerifyChoreName,
            new CheckChoreNameUniquenessRequest(name)));
    }
}