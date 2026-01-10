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

    public class ChoreBuilder(Context db)
    {
        Chore chore = new Chore();

        public ChoreBuilder WithFill(string fill)
        {
            chore.Title = fill;
            chore.AvatarUrl = fill;
            chore.Body = fill;
            return this;
        }

        public ChoreBuilder WithTitle(string title)
        {
            chore.Title = title;
            return this;
        }

        public async Task<ChoreBuilder> WithOwner(string name = "owner")
        {
            User user = await CreateAndAddUser(name, db);
            user.OwnedChores.Add(chore);
            chore.Members.Add(new ChoreMember
            {
                UserId = user.Id,
                IsAdmin = true,
            });
            return this;
        }

        public async Task<ChoreBuilder> WithAdmin(string name = "user")
        {
            User user = await CreateAndAddUser(name, db);
            chore.Members.Add(new ChoreMember
            {
                UserId = user.Id,
                IsAdmin = false,
            });
            return this;
        }

        public async Task<ChoreBuilder> WithUser(string name = "user")
        {
            User user = await CreateAndAddUser(name, db);
            chore.Members.Add(new ChoreMember
            {
                UserId = user.Id,
                IsAdmin = false,
            });
            return this;
        }

        public async Task<Chore> Build()
        {
            Assert.NotNull(chore.Title);
            if (chore.Members.Count != 0)
                await db.SaveChangesAsync();
            return chore;
        }
    }
}
