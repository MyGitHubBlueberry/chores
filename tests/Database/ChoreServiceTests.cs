using Database;
using Database.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking.Packets;

namespace Tests.Database;

//todo: add abstract test to test for privileges?
public class ChoreServiceTests
{
    CreateChoreRequest choreRequest = new(
        Title: "Chore",
        Body: "I'm test chore");

    [Theory]
    [InlineData(0)]
    [InlineData(123)]
    [InlineData(-1)]
    public async Task CreateChore_Doesnt_Work_For_Empty_User(int userId)
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();

        using (var context = new Context(options))
        {
            var service = new ChoreService(context, CancellationToken.None);
            var chore = await service.CreateChoreAsync(userId, choreRequest);
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
            var chore = await service.CreateChoreAsync(userId, choreRequest);
            Assert.NotNull(chore);
            Assert.Equal(userId, chore.OwnerId);
            Assert.Equal(choreRequest.Title, chore.Title);
            Assert.Equal(choreRequest.Body, chore.Body);
            Assert.NotEmpty(chore.Members);
        }

        using (var context = new Context(options))
        {
            Assert.NotEmpty(context.Chores);
        }
    }

    [Fact]
    public async Task DeleteChore_Not_Members_Cant_Delete()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore;
        User notMember;

        using (var context = new Context(options))
        {
            chore = await new DbTestChoreBuilder(context)
                .WithOwner().GetAwaiter().GetResult()
                .Build();
            notMember = await DbTestHelper.CreateAndAddUser("user", context);
        }

        using (var context = new Context(options))
        {
            Assert.NotEmpty(context.Chores);

            Assert.False(await new ChoreService(context, CancellationToken.None)
                    .DeleteChoreAsync(notMember.Id, chore.Id));

            Assert.NotEmpty(context.Chores);
        }
    }

    [Fact]
    public async Task DeleteChore_Owner_Can_Delete()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner().GetAwaiter().GetResult()
                .Build();

        using var context = new Context(options);
        Assert.NotEmpty(context.Chores);

        Assert.True(await new ChoreService(context, CancellationToken.None)
                .DeleteChoreAsync(chore.OwnerId, chore.Id));

        Assert.Empty(context.Chores);
    }

    [Fact]
    public async Task DeleteChore_Admin_Cant_Delete()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner().GetAwaiter().GetResult()
                .WithAdmin().GetAwaiter().GetResult()
                .Build();

        int adminId = chore.Members
            .Where(m => m.IsAdmin && m.UserId != chore.OwnerId)
            .Select(m => m.UserId)
            .First();

        using var context = new Context(options);

        Assert.NotEmpty(context.Chores);
        var service = new ChoreService(context, CancellationToken.None);
        Assert.False(await service.DeleteChoreAsync(adminId, chore.Id));
        Assert.NotEmpty(context.Chores);
    }

    [Fact]
    public async Task DeleteChore_Regular_Members_Cant_Delete()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner().GetAwaiter().GetResult()
                .WithUser().GetAwaiter().GetResult()
                .Build();
        int userId = chore.Members
            .Where(m => !m.IsAdmin)
            .Select(m => m.UserId)
            .First();

        using var context = new Context(options);
        Assert.NotEmpty(context.Chores);
        Assert.False(await new ChoreService(context, CancellationToken.None)
                .DeleteChoreAsync(userId, chore.Id));
        Assert.NotEmpty(context.Chores);
    }

    [Fact]
    public async Task UpdateDetails_Updates_Details()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner().GetAwaiter().GetResult()
                .Build();
        var request = new UpdateChoreDetailsRequest(chore.Id,
                "new", "new", "new");
        using var context = new Context(options);

        Assert.True(await new ChoreService(context, CancellationToken.None)
                .UpdateDetailsAsync(chore.OwnerId, request));
        chore = await context.Chores.FirstAsync();
        Assert.Equal(chore.Title, request.Title);
        Assert.Equal(chore.Body, request.Body);
        Assert.Equal(chore.AvatarUrl, request.AvatarUrl);
    }

    [Fact]
    public async Task UpdateDetails_Not_Resets_Properties_If_They_Are_Null()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithFill("Something")
                .WithOwner().GetAwaiter().GetResult()
                .Build();

        using var context = new Context(options);
        var request = new UpdateChoreDetailsRequest(chore.Id);
        Assert.True(await new ChoreService(context, CancellationToken.None)
                .UpdateDetailsAsync(chore.OwnerId, request));
        chore = await context.Chores.FirstAsync();
        Assert.NotNull(chore.Title);
        Assert.NotNull(chore.Body);
        Assert.NotNull(chore.AvatarUrl);
    }

    [Fact]
    public async Task UpdateDetails_Is_Not_For_Regular_Users()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        var startingValue = "Test";
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithFill(startingValue)
                .WithOwner().GetAwaiter().GetResult()
                .WithUser().GetAwaiter().GetResult()
                .Build();
        int userId = chore.Members
            .Where(m => !m.IsAdmin)
            .Select(m => m.UserId)
            .First();

        using var context = new Context(options);
        var request = new UpdateChoreDetailsRequest(chore.Id,
                "new", "new", "new");
        Assert.False(await new ChoreService(context, CancellationToken.None)
                .UpdateDetailsAsync(userId, request));
        chore = await context.Chores.FirstAsync();
        Assert.Equal(startingValue, chore.Title);
        Assert.Equal(startingValue, chore.Body);
        Assert.Equal(startingValue, chore.AvatarUrl);
    }

    [Fact]
    public async Task UpdateSchedule_Is_Not_For_Regular_Users()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner().GetAwaiter().GetResult()
                .WithUser().GetAwaiter().GetResult()
                .Build();
        var request = new UpdateChoreScheduleRequest(chore.Id,
                StartDate: DateTime.Parse("2005-12-12"),
                Interval: TimeSpan.FromDays(3),
                Duration: TimeSpan.FromDays(3));
        int userId = chore.Members
            .Where(m => !m.IsAdmin)
            .Select(m => m.UserId)
            .First();
        using var context = new Context(options);

        Assert.NotEqual(request.StartDate, chore.StartDate);
        Assert.NotEqual(request.Interval, chore.Interval);
        Assert.NotEqual(request.Duration, chore.Duration);
        Assert.False(await new ChoreService(context, CancellationToken.None)
                .UpdateScheduleAsync(userId, request));
        Assert.NotEqual(request.StartDate, chore.StartDate);
        Assert.NotEqual(request.Interval, chore.Interval);
        Assert.NotEqual(request.Duration, chore.Duration);
    }

    [Fact]
    public async Task UpdateSchedule_Updates_Schedule()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner().GetAwaiter().GetResult()
                .WithUser().GetAwaiter().GetResult()
                .Build();
        var request = new UpdateChoreScheduleRequest(chore.Id,
                StartDate: DateTime.Parse("2005-12-12"),
                Interval: TimeSpan.FromDays(3),
                Duration: TimeSpan.FromDays(3));
        using var context = new Context(options);

        Assert.NotEqual(request.StartDate, chore.StartDate);
        Assert.NotEqual(request.Interval, chore.Interval);
        Assert.NotEqual(request.Duration, chore.Duration);
        Assert.True(await new ChoreService(context, CancellationToken.None)
                .UpdateScheduleAsync(chore.OwnerId, request));
        chore = await context.Chores.FirstAsync();
        Assert.Equal(request.StartDate, chore.StartDate);
        Assert.Equal(request.Interval, chore.Interval);
        Assert.Equal(request.Duration, chore.Duration);
    }
}
