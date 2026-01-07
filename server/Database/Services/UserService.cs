using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;

namespace Database.Services;

public class UserService(Context db, CancellationToken token)
{
    //TODO: change password to be hashed
    //TODO: maybe switch to records in parameters?
    public async Task<User?> CreateUserAsync(string username, string password)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);

        try
        {
            if (await db.Users.AnyAsync(u => u.Username == username, token))
                return null;

            User user = new User
            {
                Username = username,
                Password = Encoding.UTF8.GetBytes(password) //temporary
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(token);

            await transaction.CommitAsync(token);
            return user;
        }
        catch { }
        return null;
    }

    public async Task<User?> GetByIdAsync(int id) =>
        await db.Users.FindAsync(id, token);

    public async Task<User?> GetByNameAsync(string name) =>
        await db.Users.Where(u => u.Username == name).FirstOrDefaultAsync(token);

    public async Task<bool> DeleteUserAsync(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);

        try
        {
            var result = await db.Users.Where(u => u.Id == id)
                .ExecuteDeleteAsync(token) != 0;
            if (result)
                await transaction.CommitAsync(token);
            return result;
        }
        catch { }
        return false;
    }

    public async Task<ICollection<Chore>> GetOwnedChoresByIdAsync(int id) =>
        await db.Chores
            .Where(c => c.OwnerId == id)
            .ToListAsync();

    public async Task<ICollection<ChoreMember>> GetMembershipsByIdAsync(int id) =>
        await db.ChoreMembers
            .Where(cm => cm.UserId == id)
            .ToListAsync();

    public async Task<ICollection<ChoreLog>> GetAssociatedLogsByIdAsync(int id) =>
        await db.ChoreLogs
            .Where(cl => cl.UserId == id)
            .ToListAsync();

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
