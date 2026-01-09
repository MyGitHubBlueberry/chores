using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking.Packets;

namespace Database.Services;

//TODO: return responces where necesarry
public class ChoreService(Context db)
{
    #region Core chore management
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
            .Where(ch => ch.NextMemberIdx.HasValue)  // don't allow empty chores to be started
            .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(ch => ch.IsPaused, isPaused),
                token);
        if (rows == 0)
            return false;
        await db.SaveChangesAsync(token);  //TODO: handle throws
        return true;
    }

    // TODO: next member idx
    #endregion

    //TODO: should regenerate Queue
    #region Member management
    public async Task<bool> AddMemberAsync
        (int requesterId, AddMemberRequest request)
    {
        var chore = await db.Chores
            .Include(ch => ch.Members)
            .Where(ch => ch.Id == request.ChoreId)
            .FirstOrDefaultAsync();

        if (chore is null) return false;

        bool isOwner = chore.OwnerId == requesterId;
        bool isAdmin = chore.Members.Any(m => m.UserId == requesterId && m.IsAdmin);

        if (!isOwner && !isAdmin) return false;

        int userIdToAdd;
        try
        {
            userIdToAdd = await db.Users
                .Where(u => u.Username == request.Username)
                .Select(u => u.Id)
                .FirstAsync();
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (chore.Members.Any(m => m.UserId == userIdToAdd)) return false;

        int? finalRotationOrder = request.RotationOrder;
        if (request.RotationOrder.HasValue)
        {
            var rotationMembers = chore.Members
                .Where(m => m.RotationOrder.HasValue)
                .OrderBy(m => m.RotationOrder)
                .ToList();
            var rotationOrder =
                Math.Clamp(request.RotationOrder.Value, 0, rotationMembers.Count);
            foreach (var existingMember in rotationMembers.Skip(rotationOrder))
            {
                existingMember.RotationOrder++;
            }
        }

        var member = new ChoreMember
        {
            UserId = userIdToAdd,
            ChoreId = request.ChoreId,
            IsAdmin = request.IsAdmin,
            RotationOrder = finalRotationOrder
        };

        chore.Members.Add(member);
        if (!chore.NextMemberIdx.HasValue
                && member.RotationOrder.HasValue)
            chore.NextMemberIdx = 0;  // first in the queue

        await db.SaveChangesAsync();
        return true;
    }

    //TODO: should regenerate Queue
    public async Task<bool> DeleteMemberAsync(int choreId, int requesterId, int targetUserId)
    {
        var chore = await db.Chores
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == choreId);

        if (chore is null) return false;

        bool isOwner = chore.OwnerId == requesterId;
        bool isAdmin = chore.Members.Any(m => m.UserId == requesterId && m.IsAdmin);
        bool isSelf = requesterId == targetUserId; // Are you removing yourself?

        if (!isOwner && !isAdmin && !isSelf) return false;
        if (targetUserId == chore.OwnerId && !isOwner) return false;

        var targetMember = chore.Members.FirstOrDefault(m => m.UserId == targetUserId);
        if (targetMember is null) return false;

        if (!isOwner && isAdmin && targetMember.IsAdmin && !isSelf) return false;

        if (chore.Members.Count == 1)
        {
            return await DeleteChoreAsync(choreId, requesterId);
        }

        if (targetMember.RotationOrder.HasValue)
        {
            var rotationList = chore.Members
                .Where(m => m.RotationOrder.HasValue)
                .OrderBy(m => m.RotationOrder)
                .ToList();


            foreach (var member in rotationList.Skip(targetMember.RotationOrder.Value))
            {
                member.RotationOrder--;
            }

            int count = rotationList.Count;
            if (count == 1)
            {
                chore.IsPaused = true;
                chore.NextMemberIdx = 0;
            }
            else if (chore.NextMemberIdx == count)
            {
                chore.NextMemberIdx = 0;
            }
        }

        chore.Members.Remove(targetMember);

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetAdminStatusAsync(int choreId, int requesterId, int targetId, bool isAdmin)
    {
        if (requesterId == targetId) return false;

        var chore = await db.Chores
            .Where(ch => ch.Id == choreId)
            .Include(ch => ch.Members)
            .FirstOrDefaultAsync();

        if (chore is null) return false;
        if (chore.OwnerId == targetId) return false;

        var requester = chore.Members.FirstOrDefault(m => m.UserId == requesterId);
        var target = chore.Members.FirstOrDefault(m => m.UserId == targetId);
        bool isRequesterOwner = chore.OwnerId == requesterId;

        if (requester is null || target is null) return false;

        if ((target.IsAdmin && isRequesterOwner)
                || (!target.IsAdmin && (isRequesterOwner || requester.IsAdmin)))
        {
            target.IsAdmin = isAdmin;
            await db.SaveChangesAsync();
            return true;
        }
        return false;
    }

    #endregion
}
