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
    GetChoreNameToPrivileges,

    CreateChore,
    DeleteChore,
    UpdateChoreDetails,
    UpdateChoreSchedule,
    PauseChore,
    UnpauseChore,
    VerifyChoreName,

    AddMember,
    DeleteMember,
    SetAdminStatus,

    ExtendQueueFromDays,
    ExtendQueueFromEntryCount,
    SwapQueueItems,
    SwapQueueMembers,
    InsertQueueItem,
    InsertMemberInQueue,
    DeleteQueueItem,
    DeleteQueueMember,
    RegenerateQueue,
    ChangeQueueItemInterval,
    CompleteCurrentQueue,
}
