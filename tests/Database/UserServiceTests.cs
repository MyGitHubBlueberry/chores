using Database;
using Database.Services;
using Microsoft.EntityFrameworkCore;

namespace Tests.Database;

public class UserServiceTests 
{
    [Fact]
    public async Task CreateUser_Saves_To_DB()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        string username = "test";
        string password = "123";

        using (connection) {
            using (var context = new Context(options)) {
                var service = new UserService(context, CancellationToken.None);

                var user = await service.CreateUserAsync(username, password);

                Assert.NotNull(user);
                Assert.Equal(user.Username, username);
            }

            using (var context = new Context(options)) {
                var savedUser = await context.Users
                    .FirstOrDefaultAsync(u => u.Username == username);

                Assert.NotNull(savedUser);
                Assert.Equal(username, savedUser.Username);
            }
        }
    }

    [Fact]
    public async Task Cant_Create_Duplicate_User()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        string username = "taken";
        string password = "doesn't matter";
        using (connection) 
        {
            using (var context = new Context(options)) 
            {
                var service = new UserService(context, CancellationToken.None);
                var user = await service.CreateUserAsync(username, password);

                Assert.NotNull(user);
                Assert.Equal(user.Username, username);
            }

            using (var context = new Context(options)) 
            {
                var service = new UserService(context, CancellationToken.None);
                var user = await service.CreateUserAsync(username, password);
                Assert.Null(user);
            }
        }
    }

}
