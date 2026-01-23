namespace Shared.Networking;

public enum OpCode : byte
{
    Test,

    Disconnect,

    Login,
    Register,

    GetUserById,
    GetUserByName,
    DeleteUser,
    GetOwnedChores,
    GetMemberships,
    GetAssociatedLogs,
}
