using System.Threading.Tasks;
using Networking;
using Shared.Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public class LoginModel
{
    private Client client;
    public async Task Login(LoginRequest request)
    {
        if (client.IsConnected)
        {
            await this.client.SendAsync(new SendPacket<LoginRequest>(OpCode.Login, request));
        }
    }

    public async Task Register(RegisterRequest request)
    {
        if (client.IsConnected)
        {
            await this.client.SendAsync(new SendPacket<RegisterRequest>(OpCode.Register, request));
        }
    }
}