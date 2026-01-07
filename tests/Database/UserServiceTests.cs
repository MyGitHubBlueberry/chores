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
            using (var context = new Context()) {
                var service = new UserService(context, CancellationToken.None);

                var user = await service.CreateUserAsync(username, password);

                Assert.NotNull(user);
                Assert.Equal(user.Username, username);
            }

            using (var context = new Context()) {
                var savedUser = await context.Users
                    .FirstOrDefaultAsync(u => u.Username == username);

                Assert.NotNull(savedUser);
                Assert.Equal(username, savedUser.Username);
            }
        }
    }
}
