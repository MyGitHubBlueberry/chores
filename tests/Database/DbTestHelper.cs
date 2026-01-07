using Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
}
