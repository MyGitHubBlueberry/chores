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
        client.PacketReceived += HandlePackets;
    }

    private void HandlePackets(ReadPacket packet)
    {
        switch (packet.code)
        {
            case OpCode.CreateChore:
            case OpCode.DeleteChore:
            case OpCode.UpdateChoreDetails:
                _ = GetChoresWithPrivilegesAsync(new GetChoreNameToPrivilege(session.User.Id));
                break;
            case OpCode.GetChoreNameToPrivileges:
                var result = JsonSerializer.Deserialize<Result<ICollection<ChoreNameToPrivilege>>>(packet.jsonData);
                callback?.Invoke(result.Value);
                break;
        }
    }

    public async Task GetChoresWithPrivilegesAsync(GetChoreNameToPrivilege request)
    {
        await client.SendAsync(new SendPacket<GetChoreNameToPrivilege>(OpCode.GetChoreNameToPrivileges, request));
    }
}