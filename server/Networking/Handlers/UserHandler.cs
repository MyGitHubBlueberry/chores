// using System;
// using System.Diagnostics;
// using System.Net.Sockets;
// using System.Text.Json;
// using System.Threading;
// using System.Threading.Tasks;
// using Database.Services;
// using Shared.Database.Models;
// using Shared.Networking;
// using Shared.Networking.Packets;
//
// namespace Networking.Handlers;
//
// public class UserHandler(UserService service) : IPacketHandler
// {
//     public async Task<bool> HandleAsync
//         (NetworkStream stream, ReadPacket packet, CancellationToken token = default)
//     {
//         while (!token.IsCancellationRequested)
//         {
//             switch (packet.code)
//             {
//
//             }
//         }
//         return false;
//     }
//
//     public async Task<bool> ActAsync<T, Res>
//         (NetworkStream stream, ReadPacket packet, Func<T, Task<Res>> func)
//         where Res : Result
//     {
//         T? request;
//         try
//         {
//             request = JsonSerializer.Deserialize<T>(packet.jsonData);
//         }
//         catch
//         {
//             return false;
//         }
//
//         Debug.Assert(request != null);
//
//         var responce = await func.Invoke(request);
//
//         SendPacket<Result> sendPacket = new(packet.code, responce);
//         await PacketProtocol.SendPacketAsync(stream, sendPacket);
//         return responce.IsSuccess;
//     }
// }
