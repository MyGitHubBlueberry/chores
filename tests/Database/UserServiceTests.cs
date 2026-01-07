using Database;
using Database.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;

namespace Tests.Database;

public class UserServiceTests
{
    [Fact]
    public async Task CreateUser_Saves_To_DB()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        string username = "test";
        string password = "123";

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
                var savedUser = await context.Users
                    .FirstOrDefaultAsync(u => u.Username == username);

                Assert.NotNull(savedUser);
                Assert.Equal(username, savedUser.Username);
            }
        }
    }

    [Fact]
    public async Task CreteUser_Cant_Create_Duplicate_User()
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

            using (var context = new Context(options))
            {
                Assert.Equal(1, await context.Users.CountAsync());
            }
        }
    }

    [Fact]
    public async Task GetById_Retruns_Null_When_No_Users_In_DB()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using (var context = new Context(options))
        {
            var service = new UserService(context, CancellationToken.None);
            var user = await service.GetByIdAsync(0);

            Assert.Empty(context.Users);
            Assert.Null(user);
        }
    }

    [Fact]
    public async Task GetById_Retruns_User()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        string username = "test";
        int id;

        using (var context = new Context(options))
        {
            var user = new User
            {
                Username = username,
                Password = new byte[] { 1, 2, 3 }
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            id = user.Id;
        }

        using (var context = new Context(options))
        {
            var service = new UserService(context, CancellationToken.None);
            var user = await service.GetByIdAsync(id);

            Assert.NotEmpty(context.Users);
            Assert.NotNull(user);
            Assert.Equal(username, user.Username);
        }
    }

    [Fact]
    public async Task GetById_Retruns_Right_User()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        string username = "test";
        int id;

        using (var context = new Context(options))
        {
            User[] users = new User[]{
                new User {
                    Username = username + username,
                    Password = new byte[] { 1, 2, 3 }
                },
                new User {
                    Username = username,
                    Password = new byte[] { 1, 2, 3 }
                }
            };
            context.Users.AddRange(users);
            await context.SaveChangesAsync();
            id = users[1].Id;
        }

        using (var context = new Context(options))
        {
            var service = new UserService(context, CancellationToken.None);
            var user = await service.GetByIdAsync(id);

            Assert.NotEmpty(context.Users);
            Assert.NotNull(user);
            Assert.Equal(username, user.Username);
        }
    }


    [Theory]
    [InlineData("Test")]
    [InlineData("alsdfkaslfkdj")]
    [InlineData("")]
    public async Task GetByName_Returns_Null_When_Db_Is_Empty(string username)
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        User? user;

        using (var context = new Context(options))
        {
            var service = new UserService(context, CancellationToken.None);
            user = await service.GetByNameAsync(username);

            Assert.Empty(context.Users);
        }
        Assert.Null(user);
    }

    [Theory]
    [InlineData("test")]
    [InlineData("Test")]
    [InlineData("tEst")]
    [InlineData("TEST")]
    public async Task GetByName_Is_Case_Sensetive(string username)
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        User? user;

        using (var context = new Context(options))
        {
            User[] users = new User[]{
                new User {
                    Username = "test",
                    Password = new byte[] { 1, 2, 3 }
                },
                new User {
                    Username = "Test",
                    Password = new byte[] { 1, 2, 3 }
                },
                new User {
                    Username = "tEst",
                    Password = new byte[] { 1, 2, 3 }
                },
                new User {
                    Username = "TEST",
                    Password = new byte[] { 1, 2, 3 }
                }
            };
            context.Users.AddRange(users);
            await context.SaveChangesAsync();
        }

        using (var context = new Context(options))
        {
            var service = new UserService(context, CancellationToken.None);
            user = await service.GetByNameAsync(username);

            Assert.NotEmpty(context.Users);
        }

        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
    }

    [Fact]
    public async Task DeleteUser_Deletes_User()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        User? user;
        int id;

        using (var context = new Context(options))
        {
            user = new User
            {
                Username = "test",
                Password = new byte[] { 1, 2, 3 }
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            id = user.Id;
        }

        using (var context = new Context(options))
        {
            Assert.NotEmpty(context.Users);

            var service = new UserService(context, CancellationToken.None);
            var result = await service.DeleteUserAsync(id);

            Assert.True(result);
            Assert.Empty(context.Users);
        }

        using (var context = new Context(options))
        {
            Assert.Empty(context.Users);
        }
    }
}
