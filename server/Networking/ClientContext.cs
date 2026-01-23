using System.Net.Sockets;
using Shared.Database.Models;

namespace Networking;

public class ClientContext
{
    public NetworkStream Stream { get; }
    public User? CurrentUser { get; set; } 

    public ClientContext(NetworkStream stream)
    {
        Stream = stream;
    }
}
