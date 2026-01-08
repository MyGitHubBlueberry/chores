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
        (int ownerId, CreateChoreRequest request, CancellationToken token = default)
    {
        if (!await db.Users.AnyAsync(u => u.Id == ownerId, token))
            return null;

        var chore = new Chore
        {
            OwnerId = ownerId,
            Title = request.Title,
            Body = request.Body,
            AvatarUrl = request.AvatarUrl, //todo: save in server
        };

        chore.StartDate = request.StartDate ?? chore.StartDate;
        chore.Interval = request.Interval ?? chore.Interval;
        chore.Duration = request.Duration ?? chore.Duration;

        chore.Members.Add(new ChoreMember
        {
            UserId = ownerId,
            IsAdmin = true,
        });

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
            .Where(c => c.Id == request.ChoreId &&
                    (c.OwnerId == userId || c.Members.Any(m => m.UserId == userId && m.IsAdmin)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.Title, c => request.Title ?? c.Title)
                .SetProperty(c => c.Body, c => request.Body ?? c.Body)
                .SetProperty(c => c.AvatarUrl, c => request.AvatarUrl ?? c.AvatarUrl),
            token);

        if (rows == 0)
            return false;
        await db.SaveChangesAsync(token);  //TODO: handle throws
        return true;
    }

    //TODO: should regen chore queue if any members participate in chore
    public async Task<bool> UpdateScheduleAsync
        (int userId, UpdateChoreScheduleRequest request, CancellationToken token = default)
    {
        int rows = await db.Chores
            .Where(c => c.Id == request.ChoreId &&
                    (c.OwnerId == userId || c.Members.Any(m => m.UserId == userId && m.IsAdmin)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.StartDate, c => request.StartDate ?? c.StartDate)
                .SetProperty(c => c.Interval, c => request.Interval ?? c.Interval)
                .SetProperty(c => c.Duration, c => request.Duration ?? c.Duration),
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
            .Where(ch => ch.Members.Any())  // don't allow empty chores to be started
            .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(ch => ch.IsPaused, isPaused),
                token);
        if (rows == 0)
            return false;
        await db.SaveChangesAsync(token);  //TODO: handle throws
        return true;
    }

    // TODO: next member idx
}
