using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Database.Services;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Networking.Handlers;

public class ChoreMemberHandler(ChoreMemberService service) : IPacketHandler
{
    public async Task<bool> HandleAsync(ClientContext context, ReadPacket packet, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (context.CurrentUser is null)
                return false;
            switch (packet.code)
            {
                case OpCode.AddMember:
                case OpCode.DeleteMember:
                case OpCode.SetAdminStatus:
                default: 
                    return false;
        }
    }
        return false;
    }
}
