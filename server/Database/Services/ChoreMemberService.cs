using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;
using Privileges = Database.Services.ChorePermissionService.Privileges;

namespace Database.Services;

//todo: needs shared methods
//todo: needs queue
//todo: needs chore

public class ChoreMemberService
    (Context db, ChoreQueueService qServ, ChoreService cServ, ChorePermissionService pServ)
{
    //TODO: switch to add memberS maybe pass ienumerable(async) in request
    //TODO: test it better
    public async Task<Result> AddMemberAsync
        (int requesterId, AddMemberRequest request, CancellationToken token = default)
    {
        var chore = await db.Chores
            .Include(ch => ch.Members)
            .Where(ch => ch.Id == request.ChoreId)
            .FirstOrDefaultAsync(token);
        if (chore is null) return Result.NotFound("Chore not found");

        var result = await pServ.ExistsAndSufficientPrivilegesAsync
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
            var insertionResult = !(await qServ.InsertMemberInQueueAsync
                (chore.Id, requesterId, userIdToAdd.Value, request.RotationOrder.Value)).IsSuccess;
            if (!insertionResult)
                return Result.Fail(ServiceError.DatabaseError, "Something went wrong");
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
