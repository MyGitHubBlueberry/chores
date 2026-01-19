using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Networking;

namespace Database.Services;

public class ChorePermissionService(Context db)
{
    public async Task<Result> ExistsAndSufficientPrivilegesAsync
        (int choreId, int userId, Privileges privilege)
    {
        if (!await db.Chores.AnyAsync(ch => ch.Id == choreId))
            return Result.NotFound("Chore not found");
        if (!await db.Users.AnyAsync(u => u.Id == userId))
            return Result.NotFound("User not found");
        if (!await ArePrivilegesSufficientAsync(privilege, choreId, userId))
            return Result.Forbidden();
        return Result.Success();
    }

    public async Task<bool> ArePrivilegesSufficientAsync
        (Privileges privilege, int choreId, int userId) => privilege switch
        {
            Privileges.Owner => await db.Chores
                .AnyAsync(ch => ch.Id == choreId
                        && ch.OwnerId == userId),
            Privileges.Admin => await db.Chores
                .Include(ch => ch.Members)
                .AnyAsync(ch => ch.Members
                        .Any(m => m.UserId == userId && m.IsAdmin)),
            Privileges.Member => await db.Chores
                .Include(ch => ch.Members)
                .AnyAsync(ch => ch.Members.Any(m => m.UserId == userId)),
            _ => false,
        };

    public enum Privileges
    {
        Owner,
        Admin,
        Member,
    }
}
