using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;
using Privileges = Database.Services.ChorePermissionService.Privileges;

namespace Database.Services;

public class ChoreMemberService
    (Context db, ChoreQueueService qServ, ChoreService cServ, ChorePermissionService pServ)
{
    //TODO: switch to add memberS maybe pass ienumerable(async) in request
    //TODO: test it better
    public async Task<Result> AddMembersAsync
        (int requesterId, AddMembersRequest request, CancellationToken token = default)
    {
        var chore = await db.Chores
            .Include(ch => ch.Members)
            .Where(ch => ch.Id == request.ChoreId)
            .FirstOrDefaultAsync(token);
        if (chore is null) return Result.NotFound("Chore not found");

        var authResult = await pServ.ExistsAndSufficientPrivilegesAsync
            (request.ChoreId, requesterId, Privileges.Admin);
        if (!authResult.IsSuccess) return authResult;

        if (!request.UsernamesToMemberStatuses.Any())
            return Result.Fail(ServiceError.InvalidInput, "No members to add");

        var idUsernamePairs = await db.Users
            .Where(u => request.UsernamesToMemberStatuses.Keys.Contains(u.Username))
            .ToDictionaryAsync(u => u.Id, u => u.Username, token);

        if (idUsernamePairs is null || !idUsernamePairs.Any())
            return Result.NotFound("No users with requested usernames found");
        if (idUsernamePairs.Count != request.UsernamesToMemberStatuses.Count)
            return Result.NotFound("Not all users from requested list were found");

        if (chore.Members.Any(m => idUsernamePairs.ContainsKey(m.UserId)))
            return Result.Fail(ServiceError.Conflict,
                    "At least one of requested users is already a member");

        using var transaction = await db.Database.BeginTransactionAsync(token);

        var members = idUsernamePairs.Select(pair => new ChoreMember
        {
            UserId = pair.Key,
            IsAdmin = request.UsernamesToMemberStatuses[pair.Value].IsAdmin
        });
        foreach (var member in members)
            chore.Members.Add(member);

        await db.SaveChangesAsync(token);

        var withRotationOrder = request.UsernamesToMemberStatuses
            .Where(x => x.Value.RotationOrder.HasValue)
            .Join(idUsernamePairs,
                    usernameStasus => usernameStasus.Key,
                    idUsername => idUsername.Value,
                    (usernameStasus, idUsername) => new
                    {
                        Id = idUsername.Key,
                        RotationOrder = usernameStasus.Value.RotationOrder!.Value,
                    });

        foreach (var entry in withRotationOrder)
        {
            var insertionResult = await qServ.InsertMemberInQueueAsync
                    (chore.Id, requesterId, entry.Id, entry.RotationOrder);
            if (!insertionResult.IsSuccess)
                return insertionResult;
        }

        await transaction.CommitAsync(token);
        return Result.Success();
    }

    public async Task<Result> DeleteMemberAsync
        (int choreId, int requesterId, int targetUserId, CancellationToken token = default)
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
            return await cServ.DeleteChoreAsync(choreId, requesterId);

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

        await qServ.DeleteMemberFromQueueAsync(choreId, chore.OwnerId, targetUserId);
        chore.Members.Remove(targetMember);

        await db.SaveChangesAsync(token);
        return Result.Success();
    }

    public async Task<Result> SetAdminStatusAsync
        (int choreId, int requesterId, int targetId, bool isAdmin, CancellationToken token = default)
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
}
