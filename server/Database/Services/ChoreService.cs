using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;
using Privileges = Database.Services.ChorePermissionService.Privileges;

namespace Database.Services;

//todo: assigned date for chorelog?
//TODO: add created and deleted logs as well as logs for other actions
//Todo: maybe save ownerId and chore name in logs as well as who did action

//TODO: create and handle skip requests (maybe add skip log option for deleting queue entry)

//todo: maybe move duration and interval to queue items?
//TODO: background service who will call method to cleanup missed tasks

public class ChoreService(Context db, ChoreQueueService qServ, ChorePermissionService pServ)
{
    //todo: log it
    public async Task<Result<Chore>> CreateChoreAsync
        (int ownerId, CreateChoreRequest request, CancellationToken token = default)
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

        var result = await ChangeChoreScheduleIfValidAsync(chore, request, token);
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

    //todo: log it
    public async Task<Result> DeleteChoreAsync
        (int userId, int choreId, CancellationToken token = default)
    {
        var result = await pServ.ExistsAndSufficientPrivilegesAsync(choreId, userId, Privileges.Owner);
        if (!result.IsSuccess) return result;
        return await db.Chores
            .Where(ch => ch.Id == choreId)
            .ExecuteDeleteAsync(token) != 0
                ? Result.Success()
                : Result.Fail(ServiceError.DatabaseError, "Could not delete chore");  //should never happen
    }

    public async Task<Result> UpdateDetailsAsync
        (int userId, UpdateChoreDetailsRequest request, CancellationToken token = default)
    {
        var result = await pServ.ExistsAndSufficientPrivilegesAsync
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

    //todo: test for queue regeneration and trim
    public async Task<Result> UpdateScheduleAsync
        (int userId, UpdateChoreScheduleRequest request, CancellationToken token = default)
    {
        bool shouldRegenerateQueue = false;
        var chore = await db.Chores
            .FirstOrDefaultAsync(ch => ch.Id == request.ChoreId, token);

        var result = await pServ.ExistsAndSufficientPrivilegesAsync
            (request.ChoreId, userId, Privileges.Owner);
        if (!result.IsSuccess) return result;
        Debug.Assert(chore is not null);

        if (!request.EndDate.HasValue
                && !request.Interval.HasValue
                && !request.Duration.HasValue)
            return Result.Fail(ServiceError.InvalidInput, "Request is empty");

        using var transaction = await db.Database.BeginTransactionAsync(token);

        var updateResult = await ChangeChoreScheduleIfValidAsync(chore, request, token);
        if (!updateResult.IsSuccess) return updateResult;

        bool shouldRegenerate = request.Interval.HasValue || request.Duration.HasValue;

        if (shouldRegenerateQueue)
        {
            await qServ.RegenerateQueueAsync(chore, token);
        }
        else if (request.EndDate.HasValue)
        {
            await db.ChoreQueue
                .Where(q => q.ChoreId == request.ChoreId)
                .Where(q => q.ScheduledDate >= request.EndDate)
                .ExecuteDeleteAsync(token);
        }

        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);

        return Result.Success();
    }

    public async Task<Result> PauseChoreAsync
        (int userId, int choreId, CancellationToken token = default)
    {
        var result = await pServ.ExistsAndSufficientPrivilegesAsync
            (choreId, userId, Privileges.Admin);
        if (!result.IsSuccess) return result;
        return (await db.Chores
                .Where(ch => ch.Id == choreId)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(ch => ch.IsPaused, true))) != 0
            ? Result.Success()
            : Result.Fail(ServiceError.DatabaseError, "Failed to pause the chore");
    }

    //todo: test for queue offset after unpause
    public async Task<Result> UnpauseChoreAsync
        (int userId, int choreId, CancellationToken token = default)
    {
        var chore = await db.Chores
            .Include(ch => ch.QueueItems)
            .FirstOrDefaultAsync(ch => ch.Id == choreId);

        var result = await pServ.ExistsAndSufficientPrivilegesAsync
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
    public async Task<Result> CompleteChoreAsync
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
        await qServ.ExtendQueueFromEntryCountAsync(chore, 1, token);

        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Result.Success();
    }

    //todo: test it
    public async Task ProcessMissedChoresAsync(CancellationToken token = default)
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
                await qServ.ExtendQueueFromEntryCountAsync(chore, missedItemCount);
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

    private async Task<Result> ChangeChoreScheduleIfValidAsync
        (Chore chore, CreateChoreRequest request, CancellationToken token = default)
        => await ChangeChoreScheduleIfValidAsync(chore, new Schedule(request), token);
    private async Task<Result> ChangeChoreScheduleIfValidAsync
        (Chore chore, UpdateChoreScheduleRequest request, CancellationToken token = default)
        => await ChangeChoreScheduleIfValidAsync(chore, new Schedule(request), token);
    private async Task<Result> ChangeChoreScheduleIfValidAsync
        (Chore chore, Schedule schedule, CancellationToken token = default)
    {
        TimeSpan minDuration = TimeSpan.FromHours(1);
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

            var dates = db.ChoreLogs
                .Where(l => l.ChoreId == chore.Id)
                .OrderBy(d => d)
                .Select(l => l.CompletedAt);
            DateTime? lastLogDate = await dates.AnyAsync(token)
                ? await dates.FirstAsync(token)
                : null;
            if (lastLogDate.HasValue && schedule.EndDate <= lastLogDate)
            {
                return Result.Fail(ServiceError.InvalidInput,
                        "End date can't be before previousely completed chores");
            }
        }

        if (schedule.Duration.HasValue)
        {
            if (schedule.Duration <= minDuration)
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
}
