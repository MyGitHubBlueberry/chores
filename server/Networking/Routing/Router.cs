using System.Collections.Generic;
using Networking.Handlers;
using Shared.Networking;

namespace Networking.Routing;

public class Router
{
    Dictionary<OpCode, PacketHandler> handlers = new();
    public int HandlerCount => handlers.Count;

    public PacketHandler? this[OpCode code]
    {
        get => handlers.GetValueOrDefault(code);
        set
        {
            if (value is not null)
            {
                handlers[code] = value;
            }
        }
    }
}
