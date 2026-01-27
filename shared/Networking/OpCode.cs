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

    CreateChore,
    DeleteChore,
    UpdateChoreDetails,
    UpdateChoreSchedule,
    PauseChore,
    UnpauseChore,
}
