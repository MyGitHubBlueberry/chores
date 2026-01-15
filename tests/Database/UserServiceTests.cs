using Database;
using Database.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking;

namespace Tests.Database;

public class UserServiceTests
{
    [Fact]
    public async Task CreateUser_Saves_To_DB()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        string username = "test";
        string password = "123";

        using (var context = new Context(options))
        {
            var service = new UserService(context, CancellationToken.None);

            Result<User> userResult = await service.CreateUserAsync(username, password);

            Assert.True(userResult.IsSuccess);
            Assert.NotNull(userResult.Value);
            Assert.Equal(userResult.Value.Username, username);
        }

        using (var context = new Context(options))
        {
            var savedUser = await context.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            Assert.NotNull(savedUser);
            Assert.Equal(username, savedUser.Username);
        }
    }

    [Fact]
    public async Task CreteUser_Cant_Create_Duplicate_User()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        string username = "taken";
        string password = "doesn't matter";
        using (var context = new Context(options))
        {
            var service = new UserService(context, CancellationToken.None);
            var user = await service.CreateUserAsync(username, password);

            Assert.True(user.IsSuccess);
            Assert.NotNull(user.Value);
            Assert.Equal(user.Value.Username, username);
        }

        using (var context = new Context(options))
        {
            var service = new UserService(context, CancellationToken.None);
            var user = await service.CreateUserAsync(username, password);

            Assert.False(user.IsSuccess);
            Assert.Equal(ServiceError.Conflict, user.Error);
        }

        using (var context = new Context(options))
        {
            Assert.Equal(1, await context.Users.CountAsync());
        }
    }

    [Fact]
    public async Task GetById_Retruns_Null_When_No_Users_In_DB()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = new UserService(context, CancellationToken.None);
        var user = await service.GetByIdAsync(0);

        Assert.Empty(context.Users);
        Assert.False(user.IsSuccess);
        Assert.Equal(ServiceError.NotFound, user.Error);
        Assert.Null(user.Value);
    }

    [Fact]
    public async Task GetById_Retruns_User()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        string username = "test";
        using var context = new Context(options);
        User user = await DbTestHelper.CreateAndAddUser(username, context);
        var service = new UserService(context, CancellationToken.None);

        var userResult = await service.GetByIdAsync(user.Id);

        Assert.NotEmpty(context.Users);
        Assert.True(userResult.IsSuccess);
        Assert.NotNull(userResult.Value);
        Assert.Equal(username, userResult.Value.Username);
    }

    [Fact]
    public async Task GetById_Retruns_Right_User()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        User anotherUser = await DbTestHelper.CreateAndAddUser("username", context);
        User user = await DbTestHelper.CreateAndAddUser("user", context);

        var service = new UserService(context, CancellationToken.None);
        var userResult = await service.GetByIdAsync(user.Id);

        Assert.NotEmpty(context.Users);
        Assert.True(userResult.IsSuccess);
        Assert.NotNull(userResult.Value);
        Assert.Equal(user.Username, userResult.Value.Username);
    }


    [Theory]
    [InlineData("Test")]
    [InlineData("alsdfkaslfkdj")]
    [InlineData("")]
    public async Task GetByName_Returns_Null_When_Db_Is_Empty(string username)
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);

        var service = new UserService(context, CancellationToken.None);
        var user = await service.GetByNameAsync(username);

        Assert.Empty(context.Users);
        Assert.False(user.IsSuccess);
        Assert.Equal(ServiceError.NotFound, user.Error);
    }

    [Theory]
    [InlineData("test")]
    [InlineData("Test")]
    [InlineData("tEst")]
    [InlineData("TEST")]
    public async Task GetByName_Is_Case_Sensetive(string username)
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        await DbTestHelper.CreateAndAddUser("test", context);
        await DbTestHelper.CreateAndAddUser("Test", context);
        await DbTestHelper.CreateAndAddUser("tEst", context);
        await DbTestHelper.CreateAndAddUser("TEST", context);

        var service = new UserService(context, CancellationToken.None);
        var userResult = await service.GetByNameAsync(username);

        Assert.NotEmpty(context.Users);
        Assert.True(userResult.IsSuccess);
        Assert.NotNull(userResult.Value);
        Assert.Equal(username, userResult.Value.Username);
    }

    [Fact]
    public async Task DeleteUser_Cant_Deletes_Not_Self()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        User user1 = await DbTestHelper.CreateAndAddUser("user1", context);
        User user2 = await DbTestHelper.CreateAndAddUser("user2", context);
        var service = new UserService(context, CancellationToken.None);
        var result = await service.DeleteUserAsync(user1.Id, user2.Id);
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceError.Forbidden, result.Error);
    }

    [Fact]
    public async Task DeleteUser_Deletes_User()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        User user = await DbTestHelper.CreateAndAddUser("test", context);

        Assert.NotEmpty(context.Users);
        var service = new UserService(context, CancellationToken.None);
        var result = await service.DeleteUserAsync(user.Id, user.Id);
        Assert.Equal(ServiceError.None, result.Error);
        Assert.Empty(context.Users);
    }

    [Fact]
    public async Task User_Profile_Loads_All_Related_Data()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();

        User user;

        using (var context = new Context(options))
        {
            user = new User { Username = "Worker", Password = new byte[] { 1 } };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var chore = new Chore { OwnerId = user.Id, };
            context.Chores.Add(chore);
            await context.SaveChangesAsync();

            var log = new ChoreLog { ChoreId = chore.Id, UserId = user.Id };
            context.ChoreLogs.Add(log);

            var member = new ChoreMember { ChoreId = chore.Id, UserId = user.Id, IsAdmin = true };
            context.ChoreMembers.Add(member);

            await context.SaveChangesAsync();
        }

        using (var context = new Context(options))
        {
            var service = new UserService(context, CancellationToken.None);

            var choresResult = await service.GetOwnedChoresByIdAsync(user.Id);
            var logsResult = await service.GetAssociatedLogsByIdAsync(user.Id);
            var membershipsResult = await service.GetMembershipsByIdAsync(user.Id);

            Assert.True(choresResult.IsSuccess);
            Assert.NotNull(choresResult.Value);
            Assert.Single(choresResult.Value);
            Assert.Equal(user.Id, choresResult.Value.First().OwnerId);
            Assert.True(logsResult.IsSuccess);
            Assert.NotNull(logsResult.Value);
            Assert.Single(logsResult.Value);
            Assert.Equal(user.Id, logsResult.Value.First().UserId);
            Assert.True(membershipsResult.IsSuccess);
            Assert.NotNull(membershipsResult.Value);
            Assert.Single(membershipsResult.Value);
            Assert.Equal(user.Id, membershipsResult.Value.First().UserId);
            Assert.True(membershipsResult.Value.First().IsAdmin);
        }
    }
}
