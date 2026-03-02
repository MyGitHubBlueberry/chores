using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Networking;
using Shared.Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public class MyChoresModel
{
    private readonly Client client;
    private readonly Action<ICollection<ChoreNameToPrivilege>> callback;
    private UserSessionStore session;

    public MyChoresModel(Client client, UserSessionStore session, Action<ICollection<ChoreNameToPrivilege>> callback)
    {
        this.callback = callback;
        this.client = client;
        this.session = session;
        client.PacketRecieved += HandlePackets;
    }

    private void HandlePackets(ReadPacket packet)
    {
        Console.WriteLine("MyChoresModel received packet. Packet code is: " + packet.code);
        switch (packet.code)
        {
            case OpCode.CreateChore:
            case OpCode.DeleteChore:
            case OpCode.UpdateChoreDetails:
                Console.WriteLine("Mychoresmodel requests data update");
                _ = GetChoresWithPrivilegesAsync(new GetChoreNameToPrivilege(session.User.Id));
                Console.WriteLine("Mychoresmodel requested data update");
                break;
            case OpCode.GetChoreNameToPrivileges:
                Console.WriteLine("Mychoresmodel started deserialization");
                var result = JsonSerializer.Deserialize<Result<ICollection<ChoreNameToPrivilege>>>(packet.jsonData);
                Console.WriteLine("Mychoresmodel called callback");
                callback?.Invoke(result.Value);
                break;
        }
    }

    public async Task GetChoresWithPrivilegesAsync(GetChoreNameToPrivilege request)
    {
        await client.SendAsync(new SendPacket<GetChoreNameToPrivilege>(OpCode.GetChoreNameToPrivileges, request));
    }
}