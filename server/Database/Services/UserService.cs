using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;

namespace Database.Services;

//TODO: stop swallowing token cancellation exeptions
public class UserService(Context db, CancellationToken token)
{
    //TODO: change password to be hashed
    //TODO: maybe switch to records in parameters?
    public async Task<User?> CreateUserAsync(string username, string password)
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

        return user;
    }

    public async Task<User?> GetByIdAsync(int id) =>
        await db.Users.FindAsync(id, token);

    public async Task<User?> GetByNameAsync(string name) =>
        await db.Users.Where(u => u.Username == name).FirstOrDefaultAsync(token);

    public async Task<bool> DeleteUserAsync(int id)
    {
        return await db.Users.Where(u => u.Id == id)
            .ExecuteDeleteAsync(token) != 0;
    }

    public async Task<ICollection<Chore>> GetOwnedChoresByIdAsync(int id) =>
        await db.Chores
        .Where(c => c.OwnerId == id)
        .ToListAsync(token);

    public async Task<ICollection<ChoreMember>> GetMembershipsByIdAsync(int id) =>
        await db.ChoreMembers
        .Where(cm => cm.UserId == id)
        .ToListAsync(token);

    public async Task<ICollection<ChoreLog>> GetAssociatedLogsByIdAsync(int id) =>
        await db.ChoreLogs
        .Where(cl => cl.UserId == id)
        .ToListAsync(token);

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
