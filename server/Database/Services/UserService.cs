using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking;

namespace Database.Services;

public class UserService(Context db, CancellationToken token)
{
    //TODO: change password to be hashed
    //TODO: maybe switch to records in parameters?
    public async Task<Result<User>> CreateUserAsync(string username, string password)
    {
        if (await db.Users.AnyAsync(u => u.Username == username, token))
            return Result<User>.Fail(ServiceError.Conflict, "User already exists");

        User user = new User
        {
            Username = username,
            Password = Encoding.UTF8.GetBytes(password) //temporary
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(token);

        return Result<User>.Success(user);
    }

    public async Task<Result<User>> GetByIdAsync(int id)
    {
        User? u = await db.Users.FindAsync(id, token);
        if (u is null)
        {
            return Result<User>.NotFound("User doesn't exist");
        }
        return Result<User>.Success(u);
    }

    public async Task<Result<User>> GetByNameAsync(string name)
    {
        return await db.Users.Where(u => u.Username == name)
            .FirstOrDefaultAsync(token) is User u 
                ? Result<User>.Success(u)
                : Result<User>.NotFound("User doesn't exist");
    }

    public async Task<Result> DeleteUserAsync(int requesterId, int id)
    {
        if (requesterId != id) return Result.Forbidden();
        return await db.Users.Where(u => u.Id == id)
            .ExecuteDeleteAsync(token) != 0
            ? Result.Success()
            : Result.NotFound();
    }

    public async Task<Result<ICollection<Chore>>> GetOwnedChoresByIdAsync(int id)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id))
            return Result<ICollection<Chore>>.NotFound();
        return Result<ICollection<Chore>>
            .Success(await db.Chores
                .Where(c => c.OwnerId == id)
                .ToListAsync(token));
    }

    public async Task<Result<ICollection<ChoreMember>>> GetMembershipsByIdAsync(int id)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id))
            return Result<ICollection<ChoreMember>>.NotFound();
        return Result<ICollection<ChoreMember>>
            .Success(await db.ChoreMembers
                .Where(cm => cm.UserId == id)
                .ToListAsync(token));
    }

    public async Task<Result<ICollection<ChoreLog>>> GetAssociatedLogsByIdAsync(int id)
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
