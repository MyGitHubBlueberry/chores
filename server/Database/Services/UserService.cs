using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Encryption;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Database.Services;

public class UserService(Context db)
{
    public async Task<Result<User>> RegisterAsync
        (RegisterRequest request, CancellationToken token = default)
    {
        Console.WriteLine("before any async");
        if (await db.Users.AnyAsync(u => u.Username == request.Username, token))
            return Result<User>.Fail(ServiceError.Conflict, "User already exists");

        User user = new User {
            Username = request.Username,
            PasswordHash = PasswordHasher.Hash(request.Password),
        };
        Console.WriteLine("user is null is: " + user is null);
        Console.WriteLine("before adding user");
        db.Users.Add(user);
        await db.SaveChangesAsync(token);
        
        Console.WriteLine("return from reg async");
        return Result<User>.Success(user);
    }

    public async Task<Result<User>> LoginAsync
        (LoginRequest request, CancellationToken token = default)
    {
        var userResult = await GetByNameAsync(request.Username);
        if (!userResult.IsSuccess || userResult.Value is null)
            return userResult;
        User user = userResult.Value;
        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Result<User>.Forbidden("Wrong password");
        return Result<User>.Success(user);
    }

    public async Task<Result<User>> GetByIdAsync
        (int id, CancellationToken token = default)
    {
        User? u = await db.Users.FindAsync(id, token);
        if (u is null)
        {
            return Result<User>.NotFound("User doesn't exist");
        }
        return Result<User>.Success(u);
    }

    public async Task<Result<User>> GetByNameAsync
        (string name, CancellationToken token = default)
    {
        User? u = await db.Users.Where(u => u.Username == name)
            .FirstOrDefaultAsync(token);
        return u is not null
                ? Result<User>.Success(u)
                : Result<User>.NotFound("User doesn't exist");
    }

    public async Task<Result> DeleteUserAsync
        (int requesterId, int id, CancellationToken token = default)
    {
        if (requesterId != id) return Result.Forbidden();
        return await db.Users.Where(u => u.Id == id)
            .ExecuteDeleteAsync(token) != 0
            ? Result.Success()
            : Result.NotFound();
    }

    public async Task<Result<ICollection<Chore>>> GetOwnedChoresByIdAsync
        (int id, CancellationToken token = default)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id))
            return Result<ICollection<Chore>>.NotFound();
        return Result<ICollection<Chore>>
            .Success(await db.Chores
                .Where(c => c.OwnerId == id)
                .ToListAsync(token));
    }

    public async Task<Result<ICollection<ChoreMember>>> GetMembershipsByIdAsync
        (int id, CancellationToken token = default)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id))
            return Result<ICollection<ChoreMember>>.NotFound();
        return Result<ICollection<ChoreMember>>
            .Success(await db.ChoreMembers
                .Where(cm => cm.UserId == id)
                .ToListAsync(token));
    }

    public async Task<Result<ICollection<ChoreLog>>> GetAssociatedLogsByIdAsync
        (int id, CancellationToken token = default)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id))
            return Result<ICollection<ChoreLog>>.NotFound();
        return Result<ICollection<ChoreLog>>
            .Success(await db.ChoreLogs
                .Where(cl => cl.UserId == id)
                .ToListAsync(token));
    }

    // public async Task<string> SetUserAwatarAsync(int id)
    // {
    //     await using var transaction = await db.Database.BeginTransactionAsync();
    //
    //     try
    //     {
    //         User user = await db.Users.FindAsync(id)
    //             ?? throw new KeyNotFoundException();
    //
    //         await transaction.CommitAsync();
    //     }
    //     catch
    //     {
    //     }
    // }
}
