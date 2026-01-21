using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking;
using Privileges = Database.Services.ChorePermissionService.Privileges;

namespace Database.Services;
//todo: needs shared methods

public class ChoreQueueService(Context db, ChorePermissionService pServ)
{
    public async Task<Result> ExtendQueueFromDaysAsync
        (int choreId, int days, CancellationToken token = default)
    {
        if (days <= 0)
            return Result.Fail(ServiceError.InvalidInput, "Days chould be positive");
        var chore = await db.Chores
            .Include(ch => ch.Members)
            .Where(ch => ch.Id == choreId)
            .FirstOrDefaultAsync(token);
        if (chore is null)
            return Result.NotFound("Chore not found");
        TimeSpan durationToCover = TimeSpan.FromDays(days);
        int totalItems = int
            .Max(1, (int)((durationToCover - chore.Duration) / (chore.Duration + chore.Interval) + 1));
        return await ExtendQueueFromEntryCountAsync(chore, totalItems);
    }

    public async Task<Result> ExtendQueueFromEntryCountAsync
        (int choreId, int entryCount, CancellationToken token = default)
    {
        if (entryCount <= 0)
            return Result.Fail(ServiceError.InvalidInput, "Entry count should be positive");
        var chore = await db.Chores
            .Include(ch => ch.Members)
            .Where(ch => ch.Id == choreId)
            .FirstOrDefaultAsync(token);
        if (chore is null)
            return Result.NotFound("Chore not found");
        return await ExtendQueueFromEntryCountAsync(chore, entryCount);
    }

    public async Task<Result> ExtendQueueFromEntryCountAsync
        (Chore chore, int entryCount, CancellationToken token = default)
    {
        if (!chore.CurrentQueueMemberIdx.HasValue)
            return Result.Fail(ServiceError.Conflict, "Can't regenerate chore queue without active members");
        int newQueueMemberRotationOrderIdx = chore.CurrentQueueMemberIdx.Value
            + chore.QueueItems.Count;
        int[] membersIdsFromRotaionOrder = chore.Members
            .Where(m => m.RotationOrder.HasValue)
            .OrderBy(m => m.RotationOrder)
            .Select(m => m.UserId)
            .ToArray();
        int memberCount = membersIdsFromRotaionOrder.Length;
        DateTime date = chore.QueueItems.Any()
            ? chore.QueueItems
                .OrderBy(i => i.ScheduledDate)
                .Last().ScheduledDate + chore.Duration + chore.Interval
            : (chore.StartDate < DateTime.UtcNow
                    ? DateTime.UtcNow
                    : chore.StartDate);

        for (int i = 0; i < entryCount; i++)
        {
            if (chore.EndDate.HasValue && date < chore.EndDate)
                break;
            chore.QueueItems.Add(new ChoreQueue
            {
                AssignedMemberId =
                    membersIdsFromRotaionOrder
                    [(newQueueMemberRotationOrderIdx + i) % memberCount],
                ScheduledDate = date
            });
            date += chore.Interval + chore.Duration;
        }
        await db.SaveChangesAsync(token);
        return Result.Success();
    }

    public async Task<Result> SwapQueueItemsAsync
        (int choreId, int userId, int queueItemAId, int queueItemBId, CancellationToken token = default)
    {
        var chore = await db.Chores
            .Include(ch => ch.QueueItems)
            .Where(ch => ch.Members.Any(m => m.UserId == userId && m.IsAdmin))
            .FirstOrDefaultAsync(token);
        if (chore is null) return Result.NotFound("Chore not found");
        var a = chore?.QueueItems.FirstOrDefault(q => q.Id == queueItemAId);
        var b = chore?.QueueItems.FirstOrDefault(q => q.Id == queueItemBId);
        if (a is null || b is null) return Result.NotFound($"Queue item not found");
        DateTime temp = a.ScheduledDate;
        a.ScheduledDate = b.ScheduledDate;
        b.ScheduledDate = temp;
        await db.SaveChangesAsync(token);
        return Result.Success();
    }

    public async Task<Result> SwapMembersInQueueAsync
        (int requesterId, int choreId, int userAId, int userBId, CancellationToken token = default)
    {
        var chore = await db.Chores
            .Include(ch => ch.QueueItems)
            .Include(ch => ch.Members)
            .Where(ch => ch.Members.Any(m => m.UserId == requesterId && m.IsAdmin))
            .FirstOrDefaultAsync(token);
        if (chore is null) return Result.NotFound("Chore not found");
        var a = chore.Members.FirstOrDefault(m => m.UserId == userAId);
        var b = chore.Members.FirstOrDefault(m => m.UserId == userBId);
        if (a is null || b is null)
            return Result.NotFound("Member not found");
        if (!a.RotationOrder.HasValue
            || !b.RotationOrder.HasValue)
            return Result.Fail(ServiceError.InvalidInput, "One of members misses rotation order");
        if (userBId == userAId)
            return Result.Fail(ServiceError.Conflict, "Can't swap with yourself");

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
        }
        (a.RotationOrder, b.RotationOrder) = (b.RotationOrder, a.RotationOrder);
        await db.SaveChangesAsync(token);
        return Result.Success();
    }

    public async Task<Result> InsertQueueEntryAsync
        (int choreId, int requesterId, ChoreQueue entry, CancellationToken token = default)
    {
        var chore = await db.Chores
            .Include(ch => ch.QueueItems)
            .Include(ch => ch.Members)
            .Where(ch => ch.Members.Any(m => m.UserId == requesterId && m.IsAdmin))
            .FirstOrDefaultAsync(token);
        if (chore is null) return Result.NotFound("Chore not found");
        var member = chore.Members.FirstOrDefault(m => m.UserId == entry.AssignedMemberId);
        if (member is null) return Result.NotFound("Member not found");
        if (chore.StartDate > entry.ScheduledDate)
            return Result.Fail(ServiceError.InvalidInput, "Can't add queue entry in the past");
        if (chore.EndDate < entry.ScheduledDate)
            return Result.Fail(ServiceError.InvalidInput, "Can't add queue entry after chore end");

        int rotationMemberCount = chore.Members
            .Where(m => m.RotationOrder.HasValue).Count();

        if (chore.QueueItems.Count == 0)
        {
            chore.QueueItems.Add(entry);
            await db.SaveChangesAsync(token);
            return Result.Success();
        }

        //ensures interval for the previous entry
        var prevDates = chore.QueueItems
            .Select(q => q.ScheduledDate)
            .Where(d => d < entry.ScheduledDate)
            .Order();
        if (prevDates.Any())
        {
            DateTime reservedTimeForNextChore = prevDates.Last()
                + chore.Duration + chore.Interval;
            if (reservedTimeForNextChore > entry.ScheduledDate)
            {
                entry.ScheduledDate = reservedTimeForNextChore;
            }
        }

        //ensures interval for the post entries
        var afterItems = chore.QueueItems
            .Where(q => q.ScheduledDate >= entry.ScheduledDate)
            .OrderBy(q => q.ScheduledDate);
        if (afterItems.Any())
        {
            TimeSpan interval = entry.ScheduledDate
                + chore.Duration + chore.Interval - afterItems.First().ScheduledDate;
            if (interval > TimeSpan.Zero)
            {
                foreach (var item in afterItems)
                {
                    item.ScheduledDate += interval;
                }
            }
        }
        chore.QueueItems
            .Where(i => i.ScheduledDate > chore.EndDate)
            .ToList()
            .ForEach(i => chore.QueueItems.Remove(i));
        RemoveTrailingQueueEntries(chore);
        chore.QueueItems.Add(entry);
        await db.SaveChangesAsync(token);
        return Result.Success();
    }

    public async Task<Result> InsertMemberInQueueAsync
        (int choreId, int requesterId, int memberId, int desiredOrderRotationIdx, CancellationToken token = default)
    {
        var chore = await db.Chores
            .Include(ch => ch.QueueItems)
            .Include(ch => ch.Members)
            .Where(ch => ch.Members.Any(m => m.UserId == requesterId && m.IsAdmin))
            .FirstOrDefaultAsync(token);
        if (chore is null) return Result.NotFound("Chore not found");
        var member = chore.Members.FirstOrDefault(m => m.UserId == memberId);
        if (member is null) return Result.NotFound("Member not found");

        int rotationMemberCount = chore.Members
            .Where(m => m.RotationOrder.HasValue).Count();
        desiredOrderRotationIdx = Math
            .Clamp(desiredOrderRotationIdx, 0, rotationMemberCount);
        chore.Members
            .Where(m => m.RotationOrder.HasValue
                    && m.RotationOrder >= desiredOrderRotationIdx)
            .ToList()
            .ForEach(m => m.RotationOrder++);
        member.RotationOrder = desiredOrderRotationIdx;

        if (chore.QueueItems.Count == 0)
        {
            await db.SaveChangesAsync(token);
            return Result.Success();
        }

        var orderedQueue = chore.QueueItems
            .OrderBy(q => q.ScheduledDate);
        DateTime startDate = orderedQueue
            .First().ScheduledDate;
        DateTime date = orderedQueue
            .Skip(desiredOrderRotationIdx - 1)
            .First().ScheduledDate;
        var itemsToAdd =
            new List<ChoreQueue>(orderedQueue.Count() / rotationMemberCount);

        foreach (ChoreQueue[] chunk in orderedQueue
                .Chunk(rotationMemberCount))
        {
            itemsToAdd.Add(new ChoreQueue
            {
                ScheduledDate = chunk.FirstOrDefault()?.ScheduledDate ?? date,
                AssignedMemberId = memberId
            });

            foreach (var choreQueue in chunk)
            {
                choreQueue.ScheduledDate += chore.Duration + chore.Interval;
            }
            date = chunk.Last().ScheduledDate + chore.Interval;
        }

        itemsToAdd.ForEach(i => chore.QueueItems.Add(i));
        RemoveTrailingQueueEntries(chore);
        await db.SaveChangesAsync(token);
        return Result.Success();
    }

    private void RemoveTrailingQueueEntries(Chore chore) =>
        chore.QueueItems
            .Where(i => i.ScheduledDate > chore.EndDate)
            .ToList()
            .ForEach(i => chore.QueueItems.Remove(i));

    public async Task<Result> DeleteQueueEntryAsync
        (int choreId, int requesterId, ChoreQueue entry, CancellationToken token = default)
    {
        var chore = await db.Chores
            .Include(ch => ch.QueueItems)
            .Where(ch => ch.Members.Any(m => m.UserId == requesterId && m.IsAdmin))
            .Where(ch => ch.Members.Any(m => m.UserId == entry.AssignedMemberId))
            .FirstOrDefaultAsync(token);
        if (chore is null) return Result.NotFound("Chore not found");
        if (!chore.QueueItems
                .Any(q => entry.AssignedMemberId == q.AssignedMemberId
                    && entry.ScheduledDate == q.ScheduledDate))
            return Result.NotFound("Found nothing to remove");

        var afterItems = chore.QueueItems
            .Where(q => q.ScheduledDate > entry.ScheduledDate);
        if (afterItems.Any())
        {
            TimeSpan interval = afterItems.First().ScheduledDate - entry.ScheduledDate;
            foreach (var item in afterItems)
            {
                item.ScheduledDate -= interval;
            }
        }
        chore.QueueItems.Remove(chore.QueueItems.First(q =>
                    q.ScheduledDate == entry.ScheduledDate
                    && q.AssignedMemberId == entry.AssignedMemberId));
        await db.SaveChangesAsync(token);
        return Result.Success();
    }

    public async Task<Result> DeleteMemberFromQueueAsync
        (int choreId, int requesterId, int memberId, CancellationToken token = default)
    {
        var chore = await db.Chores
            .Include(ch => ch.QueueItems)
            .Include(ch => ch.Members)
            .Where(ch => ch.Members.Any(m => m.UserId == requesterId && m.IsAdmin))
            .FirstOrDefaultAsync(token);
        if (chore is null) return Result.NotFound("Chore not found");
        var member = chore.Members
            .Where(m => m.UserId == memberId && m.RotationOrder.HasValue)
            .FirstOrDefault();
        if (member is null) return Result.NotFound("Member not found");
        if (chore.Members.Where(m => m.RotationOrder.HasValue).Count() == 1)
        {
            chore.QueueItems.Clear();
            chore.CurrentQueueMemberIdx = null;
            await db.SaveChangesAsync(token);
            return Result.Success();
        }
        TimeSpan offset = TimeSpan.Zero;
        var orderedQueue = chore.QueueItems
                .OrderBy(q => q.ScheduledDate);
        ChoreQueue? prevDelteteMember = null;

        foreach (var item in orderedQueue)
        {
            if (prevDelteteMember is not null)
            {
                offset += (item.ScheduledDate - prevDelteteMember.ScheduledDate)
                    .Duration();
                prevDelteteMember = null;
            }
            if (item.AssignedMemberId == memberId)
            {
                prevDelteteMember = item;
                continue;
            }
            item.ScheduledDate -= offset;
        }

        var members = chore.Members
            .Where(m => m.RotationOrder.HasValue
                && m.RotationOrder > member.RotationOrder);
        foreach (var m in members)
        {
            m.RotationOrder--;
        }

        db.ChoreQueue.RemoveRange(chore.QueueItems
                .Where(q => q.AssignedMemberId == memberId));

        chore.CurrentQueueMemberIdx = orderedQueue.FirstOrDefault()?.AssignedMemberId;
        member.RotationOrder = null;
        await db.SaveChangesAsync(token);
        return Result.Success();
    }

    public async Task<Result> RegenerateQueueAsync
        (int choreId, int userId, CancellationToken token = default)
    {
        var chore = await db.Chores
            .Include(ch => ch.QueueItems)
            .Where(ch => ch.Members.Any(m => m.UserId == userId && m.IsAdmin))
            .FirstOrDefaultAsync(token);
        if (chore is null) return Result.NotFound("Chore not found");

        using var transaction = await db.Database.BeginTransactionAsync(token);
        await RegenerateQueueAsync(chore);
        await transaction.CommitAsync(token);
        return Result.Success();
    }

    public async Task<Result> RegenerateQueueAsync(Chore chore, CancellationToken token = default)
    {
        int queueCount = chore.QueueItems.Count;
        chore.QueueItems.Clear();
        await db.SaveChangesAsync(token);
        var extentionResult = await ExtendQueueFromEntryCountAsync(chore, queueCount);
        if (!extentionResult.IsSuccess)
        {
            return extentionResult;
        }
        return Result.Success();
    }

    //todo: test it
    public async Task<Result> ChangeQueueEntryIntervalAsync
        (int choreId, int requesterId, int queueEntryId, TimeSpan interval, CancellationToken token = default)
    {
        var check = await pServ.ExistsAndSufficientPrivilegesAsync
            (choreId, requesterId, Privileges.Admin);
        if (!check.IsSuccess) return check;
        await db.ChoreQueue
            .Where(q => q.ChoreId == choreId)
            .OrderBy(q => q.ScheduledDate)
            .SkipWhile(q => q.Id != queueEntryId)
            .Skip(1)
            .ForEachAsync(q => q.ScheduledDate += interval, token);
        RemoveTrailingQueueEntries(await db.Chores
                .Include(ch => ch.QueueItems)
                .FirstAsync(ch => ch.Id == choreId, token));
        await db.SaveChangesAsync(token);
        return Result.Success();
    }

    //todo: test it
    public async Task<Result> CompleteCurrentQueueEntryAsync
        (int userId, int choreId, CancellationToken token = default)
    {
        using var transaction = await db.Database.BeginTransactionAsync(token);
        var chore = await db.Chores
            .FirstOrDefaultAsync(token);

        var result = await pServ.ExistsAndSufficientPrivilegesAsync
            (choreId, userId, Privileges.Member);
        if (!result.IsSuccess) return result;
        Debug.Assert(chore is not null);
        if (chore.IsPaused) return Result
            .Fail(ServiceError.Conflict, "Can't complete chores while paused");

        var queueItem = db.ChoreQueue
            .Where(q => q.ChoreId == choreId)
            .Where(q => q.AssignedMemberId == userId);
        if (!await queueItem.AnyAsync(token))
            return Result.Forbidden();
        if (await queueItem
                .Where(q => q.ScheduledDate < DateTime.UtcNow)
                .ExecuteDeleteAsync(token) == 0)
            return Result.Fail(ServiceError.Conflict, "Can't complete unstarted chore");
        chore.Logs.Add(new ChoreLog
        {
            UserId = userId,
            CompletedAt = DateTime.UtcNow,
            Status = Shared.Database.Enums.ChoreStatus.Completed,
            ChoreId = chore.Id,
            Duration = chore.Duration,
        });

        var dates = db.ChoreQueue
            .Where(q => q.ChoreId == choreId)
            .Select(q => q.ScheduledDate)
            .OrderBy(d => d);

        DateTime date = await dates.AnyAsync(token)
            ? await dates.LastAsync(token)
            : DateTime.UtcNow;

        chore.CurrentQueueMemberIdx = GetNextMemberIdx(chore);
        await ExtendQueueFromEntryCountAsync(chore, 1, token);

        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Result.Success();
    }

    //todo: test it
    public async Task ProcessMissedQueueEntriesAsync(CancellationToken token = default)
    {
        var missedItems = await db.ChoreQueue
            .Include(q => q.Chore)
            .Where(q => q.ScheduledDate + q.Chore!.Duration < DateTime.UtcNow)
            .ToListAsync(token);
        if (missedItems.Count == 0) return;

        await db.ChoreLogs.AddRangeAsync(missedItems
                .Select(i => new ChoreLog
                {
                    Duration = i.Chore!.Duration,
                    ChoreId = i.Chore.Id,
                    Status = Shared.Database.Enums.ChoreStatus.Missed,
                    CompletedAt = DateTime.UtcNow,
                    UserId = i.AssignedMemberId,
                }), token);

        await db.ChoreQueue
            .Where(q => q.ScheduledDate + q.Chore!.Duration < DateTime.UtcNow)
            .ExecuteDeleteAsync(token);

        var choreIds = missedItems.Select(i => i.ChoreId).Distinct();
        missedItems
            .GroupBy(i => i.Chore)
            .Select(i => (i.First().Chore, i.Count()))
            .Distinct()
            .ToList()
            .ForEach(async tuple =>
            {
                var (chore, missedItemCount) = tuple;
                chore!.CurrentQueueMemberIdx = GetNextMemberIdx(chore, missedItemCount);
                await ExtendQueueFromEntryCountAsync(chore, missedItemCount);
            });

        await db.SaveChangesAsync(token);
    }

    //todo: test it
    private int? GetNextMemberIdx(Chore chore, int count = 1, CancellationToken token = default)
    {
        Debug.Assert(count >= 1);
        int totalWorkers = chore.Members.Count(m => m.RotationOrder.HasValue);
        return (chore.CurrentQueueMemberIdx + count) % totalWorkers;
    }
}
