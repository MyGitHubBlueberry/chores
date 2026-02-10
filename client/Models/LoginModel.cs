using System.Diagnostics;
using System.Threading.Tasks;
using Networking;
using Shared.Networking;
using Shared.Networking.Packets;

namespace client.ViewModels;

public class LoginModel
{
    private readonly Client client;

    public LoginModel(Client client)
    {
        this.client = client;
        client.PacketRecieved += OnPacketRecieved;
    }

    private void OnPacketRecieved(ReadPacket packet)
    {
        switch (packet.code)
        {
            case OpCode.Login:
                Debug.WriteLine("client recieved login responce");
                break;
            case OpCode.Register:
                Debug.WriteLine("client recieved reg responce");
                break;
        }
    }

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