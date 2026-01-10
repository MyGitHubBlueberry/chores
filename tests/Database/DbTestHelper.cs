using Database;
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

    public static async Task<User> CreateAndAddUser(string name, Context db)
    {
        User user = new User
        {
            Username = name,
            Password = [1],
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        return user;
    }
}
