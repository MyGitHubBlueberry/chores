using System.Threading;
using System.Threading.Tasks;
using Shared.Networking;

namespace Networking.Handlers;

//todo: maybe change to class and move private methods here as protected ones
public interface IPacketHandler
{
    Task<bool> HandleAsync(ClientContext context, ReadPacket packet, CancellationToken token = default);
    OpCode[] GetHandledCodes();
}
