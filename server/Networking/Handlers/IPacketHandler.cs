using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared.Networking;

namespace Networking.Handlers;

public interface IPacketHandler
{
    Task<bool> HandleAsync(NetworkStream stream, ReadPacket packet, CancellationToken token = default);
}
