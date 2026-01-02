namespace Shared.Networking;

using System.Net;

public static class ConnectionHelper {
    public static IPEndPoint ConfigureEndPoint(int port) {
        IPHostEntry localhost = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress localIpAddress = localhost.AddressList[0];
        return new(localIpAddress, port);
    }
}
