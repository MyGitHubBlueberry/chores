using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;

namespace Database.Services;

//TODO: custom exceptions?
// User doesn't exist
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
            await db.Users.AddAsync(user, token);
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
            return await db.Users.Where(u => u.Id == id)
                .ExecuteDeleteAsync(token) != 0;
        }
        catch { }
        return false;
    }

    public async Task<ICollection<Chore>?> GetOwnedChoresByIdAsync(int id) =>
        (await db.Users
            .Include(u => u.OwnedChores)
            .FirstOrDefaultAsync(u => u.Id == id))?.OwnedChores;

    public async Task<ICollection<ChoreMember>?> GetMembershipsByIdAsync(int id) =>
        (await db.Users
            .Include(u => u.Memberships)
            .FirstOrDefaultAsync(u => u.Id == id))?.Memberships;

    public async Task<ICollection<ChoreLog>?> GetAssotiatedLogsByIdAsync(int id) =>
        (await db.Users
            .Include(u => u.Logs)
            .FirstOrDefaultAsync(u => u.Id == id))?.Logs;

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
