using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

public class PacketCodeMapper
{
    Dictionary<OpCode, Type> codeRequest = new();
    Dictionary<Type, Type> requestResult = new();
    Dictionary<Type, OpCode> resultCode = new();

    PacketCodeMapper() {
        Add<LoginRequest, User?>(OpCode.Login);
        Add<RegisterRequest, User?>(OpCode.Register);
    }

    public void Add<Req, Res>(OpCode code)
        where Req : Request
    {
        codeRequest.Add(code, typeof(Req));
        requestResult.Add(typeof(Req), typeof(Res));
        resultCode.Add(typeof(Res), code);
    }
}
