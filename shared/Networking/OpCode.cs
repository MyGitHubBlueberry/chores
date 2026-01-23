namespace Shared.Networking;

public enum OpCode : byte
{
    Test,

    Disconnect,

    Login,
    Register,

    GetUser,
    DeleteUser,
    GetOwnedChores,
    GetMemberships,
    GetAssociatedLogs,
}
