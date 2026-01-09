using Database;
using Database.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking.Packets;

namespace Tests.Database;

public class ChoreServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(123)]
    [InlineData(-1)]
    public async Task CreateChore_Doesnt_Work_For_Empty_User(int userId)
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        var request = new CreateChoreRequest("Chore");

        using (var context = new Context(options))
        {
            var service = new ChoreService(context, CancellationToken.None);
            var chore = await service.CreateChoreAsync(userId, request);
            Assert.Null(chore);
        }
        using (var context = new Context(options))
        {
            Assert.Empty(context.Chores);
        }
    }

    [Fact]
    public async Task CreateChore_Saves_In_Db()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        var request = new CreateChoreRequest(
                Title: "Chore",
                Body: "I'm test chore");
        int userId;

        using (var context = new Context(options))
        {
            var user = new User
            {
                Username = "Test",
                Password = [1],
            };
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
            userId = user.Id;
        }

        using (var context = new Context(options))
        {
            var service = new ChoreService(context, CancellationToken.None);
            var chore = await service.CreateChoreAsync(userId, request);
            Assert.NotNull(chore);
            Assert.Equal(userId, chore.OwnerId);
            Assert.Equal(request.Title, chore.Title);
            Assert.Equal(request.Body, chore.Body);
            Assert.NotEmpty(chore.Members);
        }

        using (var context = new Context(options))
        {
            Assert.NotEmpty(context.Chores);
        }
    }
}
