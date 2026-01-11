using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking.Packets;

namespace Database.Services;

//todo: don't allow for setting starting date in past
//todo: when chore is paused, keep first queueItems starting date at utcnow (maybe add background worker to add day to every queue entry when paused)
//TODO: return responces where necesarry
//TODO: background service who will call method to cleanup missed tasks
//TODO: create and handle skip and swap requests
//TODO: add verification for duration (should not be 0)
//TODO: add created and deleted logs? maybe save ownerId and chore name in logs
public class ChoreService(Context db, CancellationToken token)
{
    #region Core chore management
    public async Task<Chore?> CreateChoreAsync
        (int ownerId, CreateChoreRequest request)
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

    public async Task<bool> DeleteChoreAsync(int userId, int choreId) =>
        await db.Chores
            .Where(ch => ch.Id == choreId)
            .Where(ch => ch.OwnerId == userId)
            .ExecuteDeleteAsync(token) != 0;

    public async Task<bool> UpdateDetailsAsync
        (int userId, UpdateChoreDetailsRequest request) =>
        await db.Chores
            .Where(c => c.Id == request.ChoreId &&
                    (c.OwnerId == userId || c.Members.Any(m => m.UserId == userId && m.IsAdmin)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.Title, c => request.Title ?? c.Title)
                .SetProperty(c => c.Body, c => request.Body ?? c.Body)
                .SetProperty(c => c.AvatarUrl, c => request.AvatarUrl ?? c.AvatarUrl),
            token) != 0;

    //TODO: should regen chore queue if any members participate in chore
    public async Task<bool> UpdateScheduleAsync
        (int userId, UpdateChoreScheduleRequest request) =>
        await db.Chores
            .Where(c => c.Id == request.ChoreId &&
                    (c.OwnerId == userId || c.Members.Any(m => m.UserId == userId && m.IsAdmin)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.StartDate, c => request.StartDate == null ? c.StartDate : request.StartDate)
                .SetProperty(c => c.Interval, c => request.Interval == null ? c.Interval : request.Interval)
                .SetProperty(c => c.Duration, c => request.Duration == null ? c.Duration : request.Duration),
            token) != 0;

    public async Task<bool> SetIsPausedAsync
        (int userId, int choreId, bool isPaused) =>
        await db.Chores
            .Where(ch => ch.Id == choreId)
            .Where(ch => ch.OwnerId == choreId
                    || ch.Members.Any(m => m.UserId == userId && m.IsAdmin))
            .Where(ch => ch.CurrentQueueMemberIdx.HasValue)  // don't allow empty chores to be started
            .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(ch => ch.IsPaused, isPaused),
                token) != 0;

    public async Task<bool> CompleteChore(int userId, int choreId)
    {
        using var transaction = await db.Database.BeginTransactionAsync(token);
        var chore = await db.Chores
            .Include(ch => ch.Members)
            .Include(ch => ch.QueueItems)
            .FirstOrDefaultAsync(token);

        if (chore is null) return false;
        if (chore.IsPaused) return false;
        if (chore.Members.FirstOrDefault(m => m.UserId == userId) is null)
            return false;

        var queueItem = chore.QueueItems.FirstOrDefault();
        if (queueItem is null) return false;
        if (queueItem.AssignedMemberId != userId) return false; //maybe allow admins to complete instead?
        if (queueItem.ScheduledDate + chore.Duration < DateTime.UtcNow)
            return false;
        chore.Logs.Add(new ChoreLog
        {
            UserId = queueItem.AssignedMemberId,
            CompletedAt = DateTime.UtcNow,
            Status = Shared.Database.Enums.ChoreStatus.Completed,
            ChoreId = chore.Id,
            Duration = chore.Duration,
        });
        chore.QueueItems.Remove(queueItem);
        await db.SaveChangesAsync(token);

        var date = chore.QueueItems
            .OrderBy(i => i.ScheduledDate)
            .Select(i => i.ScheduledDate)
            .LastOrDefault();

        chore.CurrentQueueMemberIdx = GetNextMemberIdx(chore);
        chore.QueueItems.Add(new ChoreQueue
        {
            ChoreId = chore.Id,
            ScheduledDate = date + chore.Duration + chore.Interval,
            AssignedMemberId = GetNextChoreItemIdx(chore),
        });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);

        return true;
    }

    private int GetNextChoreItemIdx(Chore chore)
    {
        int idx = GetNextMemberIdx(chore) ?? -1;
        Debug.Assert(idx != -1);
        int totalWorkers = chore.Members.Count(m => m.RotationOrder.HasValue);
        int countInQueue = chore.QueueItems.Count;
        return (idx + countInQueue) % totalWorkers;
        // 1, 2, 3, 0, 1, .... 2 //id = 1
        // 1, 2, 3, 1, 0, .... 2
        // x, 2, 3, 1, 0, .... 2 // 4 total
        // x, 2, 3, 1, 0, (next id + count in q) % total workers.... 3 // 4 total 
        // x, 2, 3, 1, 0, (2 + 4) % 4
        // x, 2, 3, 1, 0, 2 == 2

        // 3, 4, 0, 1, 2 ... 3 //id = 3
        // 3, 4, 2, 0, 1 ... 3
        // x, 4, 2, 0, 1 ... 3
        // x, 4, 2, 0, 1, (next id + count in q) % total workers ... 3
        // x, 4, 2, 0, 1, (4 + 4) % 5 
        // x, 4, 2, 0, 1, 3 == 3

        // count in q % total workers 
        // 6, 0, 1, 2, 3, 4, ... 5
        // 6, 2, 0, 1, 4, 3
        // x, 2, 0, 1, 4, 3 (next id + count in q) % total workers 
        // x, 2, 0, 1, 4, 3 (0 + 5) % 7
        // x, 2, 0, 1, 4, 3 5 == 5
    }

    private int? GetNextMemberIdx(Chore chore)
    {
        int totalWorkers = chore.Members.Count(m => m.RotationOrder.HasValue);
        return (chore.CurrentQueueMemberIdx + 1) % totalWorkers;
    }

    public async Task ProcessMissedChoresAsync()
    {
        var missedItems = await db.ChoreQueue
            .Include(q => q.Chore)
            .Where(q => q.ScheduledDate + q.Chore.Duration < DateTime.UtcNow) //todo: fix warning
            .ToListAsync(token);
        //todo: maybe do this in parallel?
        foreach (var item in missedItems)
        {
            Debug.Assert(item.Chore is not null);
            item.Chore.Logs.Add(new ChoreLog
            {
                Duration = item.Chore.Duration,
                ChoreId = item.ChoreId,
                Status = Shared.Database.Enums.ChoreStatus.Missed,
                CompletedAt = DateTime.UtcNow,
                UserId = item.AssignedMemberId,
            });

            //TODO: 
            //should i let chore be missed?
            item.Chore.CurrentQueueMemberIdx = GetNextMemberIdx(item.Chore);
            item.Chore.QueueItems.Remove(item);
            // TODO: if testing doesn't pass maybe i need save here
            var date = item.Chore.QueueItems
                .OrderBy(i => i.ScheduledDate)
                .Select(i => i.ScheduledDate)
                .LastOrDefault();
            item.Chore.QueueItems.Add(new ChoreQueue
            {
                ChoreId = item.ChoreId,
                ScheduledDate = date + item.Chore.Duration + item.Chore.Interval,
                AssignedMemberId = GetNextChoreItemIdx(item.Chore),
            });
            //should i reassign chore to the member who missed the chore?
            //should i abstract this behaviour and let it be assigned on per chore basis?
        }

        if (missedItems.Count > 0)
        {
            await db.SaveChangesAsync(token);
        }
    }
    #endregion

    #region Member management
    //TODO: should regenerate Queue
    //TODO: switch to add memberS maybe pass ienumerable(async) in request
    public async Task<bool> AddMemberAsync
        (int requesterId, AddMemberRequest request)
    {
        var chore = await db.Chores
            .Include(ch => ch.Members)
            .Where(ch => ch.Id == request.ChoreId)
            .FirstOrDefaultAsync(token);

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
                .FirstAsync(token);
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
        if (!chore.CurrentQueueMemberIdx.HasValue
                && member.RotationOrder.HasValue)
            chore.CurrentQueueMemberIdx = 0;  // first in the queue

        await db.SaveChangesAsync(token);
        return true;
    }

    //TODO: should regenerate Queue
    public async Task<bool> DeleteMemberAsync
        (int choreId, int requesterId, int targetUserId)
    {
        var chore = await db.Chores
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == choreId, token);

        if (chore is null) return false;

        bool isOwner = chore.OwnerId == requesterId;
        bool isAdmin = chore.Members.Any(m => m.UserId == requesterId && m.IsAdmin);
        bool isSelf = requesterId == targetUserId;

        if (!isOwner && !isAdmin && !isSelf) return false;
        if (targetUserId == chore.OwnerId && !isOwner) return false;

        var targetMember = chore.Members.FirstOrDefault(m => m.UserId == targetUserId);
        if (targetMember is null) return false;

        if (!isOwner && isAdmin && targetMember.IsAdmin && !isSelf) return false;

        if (isOwner && isSelf)
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
                chore.CurrentQueueMemberIdx = 0;
            }
            else if (chore.CurrentQueueMemberIdx == count)
            {
                chore.CurrentQueueMemberIdx = 0;
            }
        }

        chore.Members.Remove(targetMember);

        await db.SaveChangesAsync(token);
        return true;
    }

    public async Task<bool> SetAdminStatusAsync
        (int choreId, int requesterId, int targetId, bool isAdmin)
    {
        if (requesterId == targetId) return false;

        var chore = await db.Chores
            .Where(ch => ch.Id == choreId)
            .Include(ch => ch.Members)
            .FirstOrDefaultAsync(token);

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
            await db.SaveChangesAsync(token);
            return true;
        }
        return false;
    }
    #endregion

    #region QueueManagement
    //add new queue items
    public async Task<bool> ExtendQueueAsync(int choreId, int days = default) 
    {
        var chore = await db.Chores
            .Include(ch => ch.Members)
            .Where(ch => ch.Id == choreId)
            .FirstOrDefaultAsync(token);
        if (chore is null || !chore.CurrentQueueMemberIdx.HasValue) 
            return false;
        //get true next queue member idx
        int newQueueMemberRotationOrderIdx = chore.CurrentQueueMemberIdx ?? 0 
            + chore.QueueItems.Count;
        int[] membersIdsFromRotaionOrder = chore.Members
            .Where(m => m.RotationOrder.HasValue)
            .OrderBy(m => m.RotationOrder)
            .Select(m => m.UserId)
            .ToArray();
        int memberCount = membersIdsFromRotaionOrder.Length;
        DateTime date = chore.QueueItems.LastOrDefault()?.ScheduledDate 
            ?? chore.StartDate;
        //TODO: prbbly should change this to add up to end of the mounth (account for current queued choreitems and reference their date as starting point instead of utcnow)
        if (days <= 0) 
            days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
        //generate next queue items
        var queueItems = new ChoreQueue[days];
        for (int i = 0; i < days; i++) {
            queueItems[i] = new ChoreQueue {
                AssignedMemberId = membersIdsFromRotaionOrder[i % memberCount],
                ScheduledDate = date
            };
            date += chore.Interval + chore.Duration;
            chore.QueueItems.Add(queueItems[i]);
        }
        await db.SaveChangesAsync(token);
        return true;
    }

    // swap (permanent for member swapping and swap at specific place once when requested)
    public async Task<bool> SwapQueueItemsAsync
        (int choreId, int userId, int queueItemAId, int queueItemBId)
    {
        var chore = await db.Chores
            .Include(ch => ch.QueueItems)
            .Where(ch => ch.Members.Any(m => m.UserId == userId && m.IsAdmin))
            .FirstOrDefaultAsync(token);
        var a = chore?.QueueItems.FirstOrDefault(q => q.Id == queueItemAId);
        var b = chore?.QueueItems.FirstOrDefault(q => q.Id == queueItemBId);
        if (a is null || b is null) return false;
        DateTime temp = a.ScheduledDate;
        a.ScheduledDate = b.ScheduledDate;
        b.ScheduledDate = temp;
        await db.SaveChangesAsync(token);
        return true;
    }

    public async Task<bool> SwapMembersInQueueAsync(int requesterId, int choreId, int userAId, int userBId)
    {
        var chore = await db.Chores
            .Include(ch => ch.QueueItems)
            .Include(ch => ch.Members)
            .Where(ch => ch.Members.Any(m => m.UserId == requesterId && m.IsAdmin))
            .FirstOrDefaultAsync(token);
        if (chore is null) return false;
        var a = chore.Members.FirstOrDefault(m => m.UserId == userAId);
        var b = chore.Members.FirstOrDefault(m => m.UserId == userBId);
        if (a is null || b is null
                || !a.RotationOrder.HasValue
                || !b.RotationOrder.HasValue) return false;

        if (chore.QueueItems.Count != 0) 
        {
            var exUserAQueueItemIdx = chore.QueueItems
                .Where(q => q.AssignedMemberId == a.UserId)
                .Select(q => q.Id)
                .ToHashSet();
            chore.QueueItems
                .Where(q => q.AssignedMemberId == b.UserId)
                .ToList()
                .ForEach(q => q.AssignedMemberId = a.UserId);
            chore.QueueItems
                .Where(q => exUserAQueueItemIdx.Contains(q.Id))
                .ToList()
                .ForEach(q => q.AssignedMemberId = b.UserId);
            await db.SaveChangesAsync(token);
        }
        a.RotationOrder ^= b.RotationOrder;
        b.RotationOrder ^= a.RotationOrder;
        a.RotationOrder ^= b.RotationOrder;
        return true;
    }
    // insert (insert new queue item or insert member in queue) (don't forget to shift dates)
    // delete (when removing member and when removing one queue item) ()

    #endregion
}
