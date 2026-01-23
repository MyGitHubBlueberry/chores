namespace Shared.Networking;

public enum OpCode : byte
{
    Test,

    Disconnect,

    Login,
    Register,

    DeleteUser,
    GetOwnerChores,
    GetMemberships,
    GetAssociatedLogs,
}
