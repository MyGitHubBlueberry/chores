using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Shared.Networking;

public class PacketProtocol 
{
    const int LEN_PREFIX_LENGTH = 4;
    const int HEADER_PREFIX_LENGTH = 5;
    const int OP_CODE_LENGTH = 1;
    const int NO_OFFSET = 0;
    public static async Task SendPacketAsync<T>(NetworkStream stream, SendPacket<T> packet) 
    {
        var json = JsonSerializer.Serialize(packet.data);
        byte[] payload = Encoding.UTF8.GetBytes(json);
        
        int length = payload.Length + OP_CODE_LENGTH;

        byte[] header = new byte[HEADER_PREFIX_LENGTH];
        BitConverter.TryWriteBytes(header.AsSpan(NO_OFFSET, LEN_PREFIX_LENGTH), length);
        header[LEN_PREFIX_LENGTH] = (byte)packet.code;


        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
    }

    public static async Task<ReadPacket> ReadPacket(NetworkStream stream) {
        byte[] lenBytes = new byte[LEN_PREFIX_LENGTH];

        try 
        {
            await stream.ReadExactlyAsync(lenBytes, NO_OFFSET, LEN_PREFIX_LENGTH);
        }
        catch (EndOfStreamException)
        {
            //connection lost
            return new (OpCode.Disconnect, "");
        }
        int length = BitConverter.ToInt32(lenBytes);
        if (length <= 0) return new (OpCode.Error, "");

        byte[] opCodeBuf = new byte[OP_CODE_LENGTH];
        await stream.ReadExactlyAsync(opCodeBuf, NO_OFFSET, OP_CODE_LENGTH);
        OpCode code = (OpCode)opCodeBuf[0];
        length--;

        if (length > 0) {
            byte[] payloadBuf = new byte[length];
            await stream.ReadExactlyAsync(payloadBuf, NO_OFFSET, length);

            return new (code, Encoding.UTF8.GetString(payloadBuf));
        }
        return new (OpCode.Error, "");
    }
}

public record ReadPacket(OpCode code, string jsonData);
public record SendPacket<T>(OpCode code, T data);

public enum OpCode : byte {
    Login,
    Signup,
    Test,
    Disconnect,
    Error,
}
