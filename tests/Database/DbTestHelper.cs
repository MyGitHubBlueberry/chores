using Database;
using Database.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;

namespace Tests.Database;

public static class DbTestHelper
{
    public static async Task<(SqliteConnection, DbContextOptions<Context>)> SetupTestDbAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<Context>()
            .UseSqlite(connection)
            .Options;

        using (var context = new Context(options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        return (connection, options);
    }

    public static User CreateUser(string name) => new User
    {
        Username = name,
        PasswordHash = "1234",
    };

    public static async Task<User> CreateAndAddUser(string name, Context db)
    {
        var user = CreateUser(name);
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static ChoreMemberService GetChoreMemberService(Context db)
    {
        var perm = new ChorePermissionService(db);
        var queue = new ChoreQueueService(db, perm);
        return new(db, queue, new ChoreService(db, queue, perm), perm);
    }

    public static ChoreQueueService GetChoreQueueService(Context db)
        => new(db, new ChorePermissionService(db));

    public static ChoreService GetChoreService(Context db)
    {
        var perm = new ChorePermissionService(db);
        var queue = new ChoreQueueService(db, perm);
        return new(db, queue, perm);
    }
}
