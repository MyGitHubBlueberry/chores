using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Database.Services;

//todo: add method to change intervals
//TODO: create and handle skip requests
//todo: assigned date for chorelog?
//TODO: add created and deleted logs? maybe save ownerId and chore name in logs

//todo: maybe move duration and interval to queue items?
//TODO: background service who will call method to cleanup missed tasks
public class ChoreService(Context db, CancellationToken token)
{
    #region Core chore management
    public async Task<Result<Chore>> CreateChoreAsync
        (int ownerId, CreateChoreRequest request)
    {
        if (!await db.Users.AnyAsync(u => u.Id == ownerId, token))
            return Result<Chore>.NotFound("User not found");
        if (await db.Chores.AnyAsync(ch => ch.Title == request.Title
                    && ch.OwnerId == ownerId))
            return Result<Chore>.Fail(ServiceError.Conflict, "Chore already exists");
        var chore = new Chore
        {
            OwnerId = ownerId,
            Title = request.Title,
            Body = request.Body,
            AvatarUrl = request.AvatarUrl, //todo: save in server
        };

        var result = ChangeChoreScheduleIfValid(chore, request);
        if (!result.IsSuccess)
            return Result<Chore>.FromFailedResult(result);

        chore.Members.Add(new ChoreMember
        {
            UserId = ownerId,
            IsAdmin = true,
        });

        await db.Chores.AddAsync(chore, token);

        await db.SaveChangesAsync(token);

        return Result<Chore>.Success(chore);
    }

    public async Task<Result> DeleteChoreAsync(int userId, int choreId)
    {
        var result = await ExistsAndSufficientPrivilegesAsync(choreId, userId, Privileges.Owner);
        if (!result.IsSuccess) return result;
        return await db.Chores
            .Where(ch => ch.Id == choreId)
            .ExecuteDeleteAsync(token) != 0
                ? Result.Success()
                : Result.Fail(ServiceError.DatabaseError, "Could not delete chore");  //should never happen
    }

    public async Task<Result> UpdateDetailsAsync
        (int userId, UpdateChoreDetailsRequest request)
    {
        var result = await ExistsAndSufficientPrivilegesAsync
            (request.ChoreId, userId, Privileges.Owner);
        if (!result.IsSuccess)
            return result;
        var ownerId = (await db.Chores.FindAsync(request.ChoreId))?.OwnerId;
        if (await db.Chores.AnyAsync(ch => ch.Title == request.Title
                    && ch.OwnerId == ownerId, token))
            return Result.Fail(ServiceError.Conflict,
                    "One member can't own 2 chores with the same name");

        return await db.Chores
            .Where(c => c.Id == request.ChoreId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.Title, c => request.Title ?? c.Title)
                .SetProperty(c => c.Body, c => request.Body ?? c.Body)
                .SetProperty(c => c.AvatarUrl, c => request.AvatarUrl ?? c.AvatarUrl),
            token) != 0
                ? Result.Success()
                : Result.Fail(ServiceError.DatabaseError, "Could not update chore details");  //should never happen
    }

    public async Task<Result> UpdateScheduleAsync
        (int userId, UpdateChoreScheduleRequest request)
    {
        bool shouldRegenerateQueue = false;
        var chore = await db.Chores
            .FirstOrDefaultAsync(ch => ch.Id == request.ChoreId, token);

        var result = await ExistsAndSufficientPrivilegesAsync
            (request.ChoreId, userId, Privileges.Owner);
        if (!result.IsSuccess) return result;
        Debug.Assert(chore is not null);

        if (!request.EndDate.HasValue
                && !request.Interval.HasValue
                && !request.Duration.HasValue)
            return Result.Fail(ServiceError.InvalidInput, "Request is empty");

        using var transaction = await db.Database.BeginTransactionAsync(token);
        if (request.Interval.HasValue)
        {
            chore.Interval = request.Interval.Value;
            shouldRegenerateQueue = true;
        }
        if (request.Duration.HasValue)
        {
            chore.Duration = request.Duration.Value;
            shouldRegenerateQueue = true;
        }

        if (request.EndDate.HasValue)
        {
            if (request.EndDate <= chore.StartDate)
            {
                return Result.Fail(ServiceError.InvalidInput,
                        "End date can't be before start date");
            }
            var dates = db.ChoreLogs
                .Where(l => l.ChoreId == request.ChoreId)
                .OrderBy(d => d)
                .Select(l => l.CompletedAt);
            DateTime? lastLogDate = await dates.AnyAsync()
                ? await dates.FirstAsync()
                : null;
            if (lastLogDate.HasValue && request.EndDate <= lastLogDate)
            {
                return Result.Fail(ServiceError.InvalidInput,
                        "End date can't be before previousely completed chores");
            }

            if (!shouldRegenerateQueue)
            {
                await db.ChoreQueue
                    .Where(q => q.ChoreId == request.ChoreId)
                    .Where(q => q.ScheduledDate >= request.EndDate)
                    .ExecuteDeleteAsync(token);
            }

            chore.EndDate = request.EndDate;
        }

        if (shouldRegenerateQueue)
        {
            await RegenerateQueueAsync(chore);
        }

        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);

        return Result<Chore>.Success(chore);
    }

    //todo: test it
    public async Task<Result> PauseChoreAsync
        (int userId, int choreId)
    {
        var result = await ExistsAndSufficientPrivilegesAsync
            (choreId, userId, Privileges.Admin);
        if (!result.IsSuccess) return result;
        return (await db.Chores
                .Where(ch => ch.Id == choreId)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(ch => ch.IsPaused, true))) != 0
            ? Result.Success()
            : Result.Fail(ServiceError.DatabaseError, "Failed to pause the chore");
    }

    //todo: test it
    public async Task<Result> UnpauseChoreAsync
        (int userId, int choreId)
    {
        var chore = await db.Chores
            .Include(ch => ch.QueueItems)
            .FirstOrDefaultAsync(ch => ch.Id == choreId);

        var result = await ExistsAndSufficientPrivilegesAsync
            (choreId, userId, Privileges.Admin);
        if (!result.IsSuccess) return result;

        Debug.Assert(chore is not null);

        if (!chore.IsPaused) return Result.Success();

        if (chore.EndDate.HasValue && chore.EndDate <= DateTime.UtcNow)
            return Result.Fail(ServiceError.Conflict, "Can't unpause ended chore");
        if (!await db.ChoreMembers.AnyAsync(m => m.ChoreId == choreId
                    && m.RotationOrder.HasValue))
            return Result.Fail(ServiceError.InvalidInput, "Can't unpause chore without active members");

        var dates = db.ChoreQueue
            .Where(q => q.ChoreId == choreId
                    && q.ScheduledDate < DateTime.UtcNow)
            .Select(q => q.ScheduledDate);
        if (await dates.AnyAsync(token))
        {
            var minDate = await dates.Order().FirstAsync(token);
            var offset = DateTime.UtcNow - minDate;
            await db.ChoreQueue
                .Where(q => q.ChoreId == choreId)
                .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(q => q.ScheduledDate, q => q.ScheduledDate + offset),
                            token);
        }

        await db.SaveChangesAsync(token);

        return Result.Success();
    }

    //todo: test it
    public async Task<Result> CompleteChoreAsync(int userId, int choreId)
    {
        using var transaction = await db.Database.BeginTransactionAsync(token);
        var chore = await db.Chores
            .FirstOrDefaultAsync(token);

        var result = await ExistsAndSufficientPrivilegesAsync
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

        DateTime date = await dates.AnyAsync()
            ? await dates.LastAsync()
            : DateTime.UtcNow;

        chore.CurrentQueueMemberIdx = GetNextMemberIdx(chore);
        await ExtendQueueFromEntryCountAsync(chore, 1);

        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Result.Success();
    }

    //todo: test it
    private int? GetNextMemberIdx(Chore chore, int count = 1)
    {
        Debug.Assert(count >= 1);
        int totalWorkers = chore.Members.Count(m => m.RotationOrder.HasValue);
        return (chore.CurrentQueueMemberIdx + count) % totalWorkers;
    }

    //todo: test it
    public async Task ProcessMissedChoresAsync()
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
    #endregion

    #region Member management
    //TODO: switch to add memberS maybe pass ienumerable(async) in request
    //TODO: test it better
    public async Task<Result> AddMemberAsync
        (int requesterId, AddMemberRequest request)
    {
        var chore = await db.Chores
            .Include(ch => ch.Members)
            .Where(ch => ch.Id == request.ChoreId)
            .FirstOrDefaultAsync(token);
        if (chore is null) return Result.NotFound("Chore not found");

        var result = await ExistsAndSufficientPrivilegesAsync
            (request.ChoreId, requesterId, Privileges.Admin);
        if (!result.IsSuccess) return result;

        var userIdToAdd = db.Users
            .FirstOrDefault(u => u.Username == request.Username)?.Id;
        if (userIdToAdd is null) return Result.NotFound("Requested user doesn't exist");

        if (chore.Members.Any(m => m.UserId == userIdToAdd))
            return Result.Fail(ServiceError.Conflict, "User is already in the chore");

        var member = new ChoreMember
        {
            UserId = userIdToAdd.Value,
            ChoreId = request.ChoreId,
            IsAdmin = request.IsAdmin,
        };

        using var transaction = await db.Database.BeginTransactionAsync(token);

        chore.Members.Add(member);
        await db.SaveChangesAsync(token);

        if (request.RotationOrder.HasValue)
        {
            var insertionResult = !(await InsertMemberInQueueAsync
                (chore.Id, requesterId, userIdToAdd.Value, request.RotationOrder.Value)).IsSuccess;
            if (!insertionResult)
                return Result.Fail(ServiceError.DatabaseError, "Something went wrong");
        }
        await transaction.CommitAsync(token);
        return Result.Success();
    }

    public async Task<Result> DeleteMemberAsync
        (int choreId, int requesterId, int targetUserId)
    {
        var chore = await db.Chores
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == choreId, token);

        if (chore is null) return Result.NotFound("Chore not found");

        bool isOwner = chore.OwnerId == requesterId;
        bool isAdmin = chore.Members.Any(m => m.UserId == requesterId && m.IsAdmin);
        bool isSelf = requesterId == targetUserId;

        if (!isOwner && !isAdmin && !isSelf) return Result.Forbidden();
        if (targetUserId == chore.OwnerId && !isOwner) return Result.Forbidden();

        var targetMember = chore.Members.FirstOrDefault(m => m.UserId == targetUserId);
        if (targetMember is null) return Result.NotFound("Member not found");

        if (!isOwner && isAdmin && targetMember.IsAdmin && !isSelf)
            return Result.Forbidden("Admins can't remove other admins");

        if (isOwner && isSelf)
            return await DeleteChoreAsync(choreId, requesterId);

        if (targetMember.RotationOrder.HasValue)
        {
            var rotationList = chore.Members
                .Where(m => m.RotationOrder.HasValue
                        && m.RotationOrder > targetMember.RotationOrder)
                .OrderBy(m => m.RotationOrder);


            foreach (var member in rotationList)
            {
                member.RotationOrder--;
            }
        }

        await DeleteMemberFromQueueAsync(choreId, chore.OwnerId, targetUserId);
        chore.Members.Remove(targetMember);

        await db.SaveChangesAsync(token);
        return Result.Success();
    }

    public async Task<Result> SetAdminStatusAsync
        (int choreId, int requesterId, int targetId, bool isAdmin)
    {
        if (requesterId == targetId)
            return Result.Fail(ServiceError.Conflict, "Can't change admin status of yourself");

        var chore = await db.Chores
            .Where(ch => ch.Id == choreId)
            .Include(ch => ch.Members)
            .FirstOrDefaultAsync(token);

        if (chore is null) return Result.NotFound("Chore not found");
        if (chore.OwnerId == targetId)
            return Result.Fail(ServiceError.InvalidInput, "Owner admin status can't be changed");

        var requester = chore.Members.FirstOrDefault(m => m.UserId == requesterId);
        var target = chore.Members.FirstOrDefault(m => m.UserId == targetId);
        bool isRequesterOwner = chore.OwnerId == requesterId;

        if (requester is null || target is null) return Result.NotFound("User not found");

        if ((target.IsAdmin && isRequesterOwner)
                || (!target.IsAdmin && requester.IsAdmin))
        {
            target.IsAdmin = isAdmin;
            await db.SaveChangesAsync(token);
            return Result.Success();
        }
        return Result.Forbidden();
    }
    #endregion

    #region QueueManagement
    public async Task<Result> ExtendQueueFromDaysAsync(int choreId, int days)
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

    public async Task<Result> ExtendQueueFromEntryCountAsync(int choreId, int entryCount)
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

    private async Task<Result> ExtendQueueFromEntryCountAsync(Chore chore, int entryCount)
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
        (int choreId, int userId, int queueItemAId, int queueItemBId)
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
        (int requesterId, int choreId, int userAId, int userBId)
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
        (int choreId, int requesterId, ChoreQueue entry)
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
        (int choreId, int requesterId, int memberId, int desiredOrderRotationIdx)
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
        (int choreId, int requesterId, ChoreQueue entry)
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
        (int choreId, int requesterId, int memberId)
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

        chore.CurrentQueueMemberIdx = orderedQueue.First().AssignedMemberId;
        member.RotationOrder = null;
        await db.SaveChangesAsync(token);
        return Result.Success();
    }

    public async Task<Result> RegenerateQueueAsync
        (int choreId, int userId)
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

    private async Task<Result> RegenerateQueueAsync(Chore chore)
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
    #endregion

    private async Task<bool> ArePrivilegesSufficientAsync
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

    enum Privileges
    {
        Owner,
        Admin,
        Member,
    }

    private Result ChangeChoreScheduleIfValid(Chore chore, CreateChoreRequest request)
        => ChangeChoreScheduleIfValid(chore, new Schedule(request));
    private Result ChangeChoreScheduleIfValid(Chore chore, UpdateChoreScheduleRequest request)
        => ChangeChoreScheduleIfValid(chore, new Schedule(request));
    private Result ChangeChoreScheduleIfValid(Chore chore, Schedule schedule)
    {
        if (schedule.StartDate.HasValue)
        {
            if (DateTime.UtcNow.Date > schedule.StartDate.Value.Date)
            {
                return Result
                    .Fail(ServiceError.InvalidInput, "Start date can't be in the past");
            }
        }

        if (schedule.EndDate.HasValue)
        {
            if (DateTime.UtcNow.Date > schedule.EndDate.Value.Date)
            {
                return Result
                    .Fail(ServiceError.InvalidInput, "End date can't be in the past");
            }

            if (schedule.EndDate <= (schedule.StartDate.HasValue
                        ? schedule.StartDate
                        : chore.StartDate))
            {
                return Result
                    .Fail(ServiceError.InvalidInput, "End date can't be before start date");
            }
        }

        if (schedule.Duration.HasValue)
        {
            if (schedule.Duration == TimeSpan.Zero)
            {
                return Result
                    .Fail(ServiceError.InvalidInput, "Duration can't be zero");
            }
        }
        chore.StartDate = schedule.StartDate ?? chore.StartDate;
        chore.EndDate = schedule.EndDate ?? chore.EndDate;
        chore.Duration = schedule.Duration ?? chore.Duration;
        chore.Interval = schedule.Interval ?? chore.Interval;
        return Result.Success();
    }

    private record Schedule
    {
        public DateTime? EndDate = null;
        public DateTime? StartDate = null;
        public TimeSpan? Duration = null;
        public TimeSpan? Interval = null;

        public Schedule(CreateChoreRequest request)
        {
            EndDate = request.EndDate;
            StartDate = request.StartDate;
            Duration = request.Duration;
            Interval = request.Interval;
        }

        public Schedule(UpdateChoreScheduleRequest request)
        {
            EndDate = request.EndDate;
            Duration = request.Duration;
            Interval = request.Interval;
        }

        public Schedule(Chore chore)
        {
            EndDate = chore.EndDate;
            StartDate = chore.StartDate;
            Duration = chore.Duration;
            Interval = chore.Interval;
        }
    }

    private async Task<Result> ExistsAndSufficientPrivilegesAsync
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
}
