using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking.Packets;

namespace Database.Services;

public class ChoreService(Context db)
{
    public async Task<Chore?> CreateChoreAsync
        (int ownerId, string title, CancellationToken token = default)
    {
        if (!await db.Users.AnyAsync(u => u.Id == ownerId, token))
            return null;

        var chore = new Chore
        {
            Title = title,
            OwnerId = ownerId,
        };
        await db.Chores.AddAsync(chore, token);
        await db.SaveChangesAsync(token);  //TODO: handle throws
        return chore;
    }

    public async Task<bool> DeleteChoreAsync
        (int userId, int choreId, CancellationToken token = default)
    {
        var deletedRows = await db.Chores
            .Where(ch => ch.Id == choreId)
            .Where(ch => ch.OwnerId == userId
                    || ch.Members.Any(m => m.UserId == userId && m.IsAdmin))
            .ExecuteDeleteAsync(token);
        if (deletedRows == 0)
            return false;
        await db.SaveChangesAsync(token);  //TODO: handle throws
        return true;
    }


    public async Task<bool> UpdateDetailsAsync
        (int userId, UpdateChoreDetailsRequest request, CancellationToken token = default)
    {
        int rows = await db.Chores
            .Where(c => c.Id == request.ChoreId && c.OwnerId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.Title, request.Title)
                .SetProperty(c => c.Body, request.Body)
                .SetProperty(c => c.AvatarUrl, request.AvatarUrl),
            token);

        if (rows == 0)
            return false;
        await db.SaveChangesAsync(token);  //TODO: handle throws
        return true;
    }

    public async Task<bool> SetIsPausedAsync
        (int userId, int choreId, bool isPaused, CancellationToken token = default)
    {
        int rows = await db.Chores
            .Where(ch => ch.Id == choreId && ch.OwnerId == choreId)
            .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(ch => ch.IsPaused, isPaused),
                token);
        if (rows == 0)
            return false;
        await db.SaveChangesAsync(token);  //TODO: handle throws
        return true;
    }

    // TODO
    // public async Task<bool> UpdateScheduleAsync
    //     (int userId, UpdateChoreScheduleRequest ,CancellationToken token = default)
    // {
    // }
}
