using System.Diagnostics;
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

    ///<summary>Sends packet through stream</summary>
    ///<exception cref="NotSupportedException">Thrown in packet.data serialization is not supported</exception>
    public static async Task SendPacketAsync<T>(NetworkStream stream, SendPacket<T> packet)
    {
        Debug.Assert(stream is not null && packet is not null);
        Debug.Assert(stream.CanWrite);

        string json = JsonSerializer.Serialize(packet.data);
        byte[] payload = Encoding.UTF8.GetBytes(json);

        int length = payload.Length + OP_CODE_LENGTH;

        byte[] header = new byte[HEADER_PREFIX_LENGTH];
        BitConverter.TryWriteBytes(header.AsSpan(NO_OFFSET, LEN_PREFIX_LENGTH), length);
        header[LEN_PREFIX_LENGTH] = (byte)packet.code;

        try
        {
            await stream.WriteAsync(header);
            await stream.WriteAsync(payload);
        }
        catch { }
    }

    public static async Task<ReadPacket> ReadPacket(NetworkStream stream, CancellationToken token = default)
    {
        Debug.Assert(stream.CanRead);

        byte[] lenBytes = new byte[LEN_PREFIX_LENGTH];

        try
        {
            await stream.ReadExactlyAsync(lenBytes, NO_OFFSET, LEN_PREFIX_LENGTH, token);
        }
        catch (Exception e)
            when (e is EndOfStreamException or OperationCanceledException)
        {
            return new(OpCode.Disconnect, e.Message);
        }

        int length = BitConverter.ToInt32(lenBytes);
        Debug.Assert(length >= HEADER_PREFIX_LENGTH);

        byte[] opCodeBuf = new byte[OP_CODE_LENGTH];
        try
        {
            await stream.ReadExactlyAsync(opCodeBuf, NO_OFFSET, OP_CODE_LENGTH, token);
        }
        catch (Exception e)
            when (e is EndOfStreamException or OperationCanceledException)
        {
            return new(OpCode.Disconnect, e.Message);
        }

        OpCode code = (OpCode)opCodeBuf[0];
        length--;

        byte[] payloadBuf = new byte[length];
        try
        {
            await stream.ReadExactlyAsync(payloadBuf, NO_OFFSET, length, token);
        }
        catch (Exception e)
            when (e is EndOfStreamException or OperationCanceledException)
        {
            return new(OpCode.Disconnect, e.Message);
        }

        return new(code, Encoding.UTF8.GetString(payloadBuf));
    }
}

public record ReadPacket(OpCode code, string jsonData);
public record SendPacket<T>(OpCode code, T data);

public enum OpCode : byte
{
    Login,
    Signup,
    Test,
    Disconnect,
}
