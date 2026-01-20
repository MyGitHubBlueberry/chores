using Database;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Tests.Database;

//todo: add abstract test to test for privileges?
[Trait("Database", "ChoreService")]
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
            var service = DbTestHelper.GetChoreService(context);
            var result = await service.CreateChoreAsync(userId, choreRequest);
            Assert.False(result.IsSuccess);
            Assert.Equal(ServiceError.NotFound, result.Error);
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
        using var context = new Context(options);
        User user = await DbTestHelper.CreateAndAddUser("user", context);

        var service = DbTestHelper.GetChoreService(context);
        var choreResult = await service.CreateChoreAsync(user.Id, choreRequest);
        Assert.True(choreResult.IsSuccess);
        Assert.NotNull(choreResult.Value);
        Assert.Equal(user.Id, choreResult.Value.OwnerId);
        Assert.Equal(choreRequest.Title, choreResult.Value.Title);
        Assert.Equal(choreRequest.Body, choreResult.Value.Body);
        Assert.NotEmpty(choreResult.Value.Members);
    }

    [Fact]
    public async Task CreateChore_Cant_Create_Two_Same_Chores()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        User user = await DbTestHelper.CreateAndAddUser("user", context);

        var service = DbTestHelper.GetChoreService(context);
        var choreResult = await service.CreateChoreAsync(user.Id, choreRequest);
        Assert.True(choreResult.IsSuccess);
        choreResult = await service.CreateChoreAsync(user.Id, choreRequest);
        Assert.False(choreResult.IsSuccess);
        Assert.Null(choreResult.Value);
        Assert.Equal(ServiceError.Conflict, choreResult.Error);
    }

    [Fact]
    public async Task CreateChore_Cant_Create_Chore_In_Past()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        User user = await DbTestHelper.CreateAndAddUser("user", context);

        var service = DbTestHelper.GetChoreService(context);
        CreateChoreRequest request = new(
                Title: "Chore",
                StartDate: DateTime.Parse("2005-01-01"));
        var choreResult = await service.CreateChoreAsync(user.Id, request);
        Assert.False(choreResult.IsSuccess);
        Assert.Equal(ServiceError.InvalidInput, choreResult.Error);
    }

    [Fact]
    public async Task CreateChore_Cant_Create_Chore_With_Zero_Duration()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        User user = await DbTestHelper.CreateAndAddUser("user", context);

        var service = DbTestHelper.GetChoreService(context);
        CreateChoreRequest request = new(
                Title: "Chore",
                Duration: TimeSpan.Zero);
        var choreResult = await service.CreateChoreAsync(user.Id, request);
        Assert.False(choreResult.IsSuccess);
        Assert.Equal(ServiceError.InvalidInput, choreResult.Error);
    }

    [Fact]
    public async Task CreateChore_Cant_Create_Chore_Ending_In_Past()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        User user = await DbTestHelper.CreateAndAddUser("user", context);

        var service = DbTestHelper.GetChoreService(context);
        CreateChoreRequest request = new(
                Title: "Chore",
                EndDate: DateTime.Parse("2005-01-01"));
        var choreResult = await service.CreateChoreAsync(user.Id, request);
        Assert.False(choreResult.IsSuccess);
        Assert.Equal(ServiceError.InvalidInput, choreResult.Error);
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
                .WithOwner()
                .BuildAsync();
            notMember = await DbTestHelper.CreateAndAddUser("user", context);
        }

        using (var context = new Context(options))
        {
            Assert.NotEmpty(context.Chores);

            var result = await DbTestHelper.GetChoreService(context)
                    .DeleteChoreAsync(notMember.Id, chore.Id);
            Assert.False(result.IsSuccess);
            Assert.Equal(ServiceError.Forbidden, result.Error);

            Assert.NotEmpty(context.Chores);
        }
    }

    [Fact]
    public async Task DeleteChore_Owner_Can_Delete()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner()
                .BuildAsync();

        using var context = new Context(options);
        Assert.NotEmpty(context.Chores);

        var result = await DbTestHelper.GetChoreService(context)
                .DeleteChoreAsync(chore.OwnerId, chore.Id);
        Assert.True(result.IsSuccess);

        Assert.Empty(context.Chores);
    }

    [Fact]
    public async Task DeleteChore_Admin_Cant_Delete()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner()
                .WithAdmin()
                .BuildAsync();

        int adminId = chore.Members
            .Where(m => m.IsAdmin && m.UserId != chore.OwnerId)
            .Select(m => m.UserId)
            .First();

        using var context = new Context(options);

        Assert.NotEmpty(context.Chores);
        var service = DbTestHelper.GetChoreService(context);
        var result = await service.DeleteChoreAsync(adminId, chore.Id);
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceError.Forbidden, result.Error);
        Assert.NotEmpty(context.Chores);
    }

    [Fact]
    public async Task DeleteChore_Regular_Members_Cant_Delete()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner()
                .WithMember()
                .BuildAsync();
        int userId = chore.Members
            .Where(m => !m.IsAdmin)
            .Select(m => m.UserId)
            .First();

        using var context = new Context(options);
        Assert.NotEmpty(context.Chores);
        var result = await DbTestHelper.GetChoreService(context)
                .DeleteChoreAsync(userId, chore.Id);
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceError.Forbidden, result.Error);
        Assert.NotEmpty(context.Chores);
    }

    [Fact]
    public async Task UpdateDetails_Cant_Update_To_Duplicate_Chore()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner()
                .BuildAsync();
        using var context = new Context(options);
        var request = new UpdateChoreDetailsRequest(chore.Id, "new");

        Assert.True((await DbTestHelper.GetChoreService(context)
            .UpdateDetailsAsync(chore.OwnerId, request)).IsSuccess);
        Result result = await DbTestHelper.GetChoreService(context)
            .UpdateDetailsAsync(chore.OwnerId, request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceError.Conflict, result.Error);
    }
    [Fact]
    public async Task UpdateDetails_Updates_Details()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner()
                .BuildAsync();
        var request = new UpdateChoreDetailsRequest(chore.Id,
                "new", "new", "new");
        Result result;

        using (var context = new Context(options))
        {
            result = await DbTestHelper.GetChoreService(context)
                .UpdateDetailsAsync(chore.OwnerId, request);
        }

        using (var context = new Context(options))
        {
            chore = await context.Chores.FirstAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal(request.Title, chore.Title);
            Assert.Equal(request.Body, chore.Body);
            Assert.Equal(request.AvatarUrl, chore.AvatarUrl);
        }
    }

    [Fact]
    public async Task UpdateDetails_Not_Resets_Properties_If_They_Are_Null()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithFill("Something")
                .WithOwner()
                .BuildAsync();

        using var context = new Context(options);
        var request = new UpdateChoreDetailsRequest(chore.Id);
        var responce = await DbTestHelper.GetChoreService(context)
                .UpdateDetailsAsync(chore.OwnerId, request);
        Assert.True(responce.IsSuccess);
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
                .WithOwner()
                .WithMember()
                .BuildAsync();
        int userId = chore.Members
            .Where(m => !m.IsAdmin)
            .Select(m => m.UserId)
            .First();

        using var context = new Context(options);
        var request = new UpdateChoreDetailsRequest(chore.Id,
                "new", "new", "new");
        var responce = await DbTestHelper.GetChoreService(context)
                .UpdateDetailsAsync(userId, request);
        Assert.False(responce.IsSuccess);
        Assert.Equal(ServiceError.Forbidden, responce.Error);
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
                .WithOwner()
                .WithMember()
                .BuildAsync();
        var request = new UpdateChoreScheduleRequest(chore.Id,
                EndDate: DateTime.Parse("2005-12-12"),
                Interval: TimeSpan.FromDays(3),
                Duration: TimeSpan.FromDays(3));
        int userId = chore.Members
            .Where(m => !m.IsAdmin)
            .Select(m => m.UserId)
            .First();
        using var context = new Context(options);

        Assert.NotEqual(request.EndDate, chore.StartDate);
        Assert.NotEqual(request.Interval, chore.Interval);
        Assert.NotEqual(request.Duration, chore.Duration);
        var result = await DbTestHelper.GetChoreService(context)
                .UpdateScheduleAsync(userId, request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceError.Forbidden, result.Error);
        Assert.NotEqual(request.EndDate, chore.StartDate);
        Assert.NotEqual(request.Interval, chore.Interval);
        Assert.NotEqual(request.Duration, chore.Duration);
    }

    [Fact]
    public async Task UpdateSchedule_Fails_If_Request_Is_Empty()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner()
                .BuildAsync();
        var request = new UpdateChoreScheduleRequest(chore.Id);
        using var context = new Context(options);

        Assert.NotEqual(request.Interval, chore.Interval);
        Assert.NotEqual(request.Duration, chore.Duration);
        var responce = await DbTestHelper.GetChoreService(context)
                .UpdateScheduleAsync(chore.OwnerId, request);
        Assert.False(responce.IsSuccess);
        Assert.Equal(ServiceError.InvalidInput, responce.Error);
        chore = await context.Chores.FirstAsync();
        Assert.NotEqual(request.Interval, chore.Interval);
        Assert.NotEqual(request.Duration, chore.Duration);
    }

    [Fact]
    public async Task UpdateSchedule_Rejects_EndDate_Before_StartDate()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner()
                .WithStartDate(DateTime.UtcNow)
                .BuildAsync();
        var request = new UpdateChoreScheduleRequest(chore.Id,
                EndDate: DateTime.UtcNow.Date - TimeSpan.FromDays(1));
        using var context = new Context(options);

        Assert.NotEqual(request.EndDate, chore.EndDate);
        Assert.NotEqual(request.Interval, chore.Interval);
        Assert.NotEqual(request.Duration, chore.Duration);
        var responce = await DbTestHelper.GetChoreService(context)
                .UpdateScheduleAsync(chore.OwnerId, request);
        Assert.False(responce.IsSuccess);
        Assert.Equal(ServiceError.InvalidInput, responce.Error);
        chore = await context.Chores.FirstAsync();
        Assert.NotEqual(request.EndDate, chore.EndDate);
        Assert.NotEqual(request.Interval, chore.Interval);
        Assert.NotEqual(request.Duration, chore.Duration);
    }

    [Fact]
    public async Task UpdateSchedule_Updates_Schedule()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner()
                .WithMember()
                .BuildAsync();
        var request = new UpdateChoreScheduleRequest(chore.Id,
                EndDate: DateTime.Parse("2035-12-12"),
                Interval: TimeSpan.FromDays(3),
                Duration: TimeSpan.FromDays(3));
        using var context = new Context(options);

        Assert.NotEqual(request.EndDate, chore.EndDate);
        Assert.NotEqual(request.Interval, chore.Interval);
        Assert.NotEqual(request.Duration, chore.Duration);
        Assert.True((await DbTestHelper.GetChoreService(context)
                .UpdateScheduleAsync(chore.OwnerId, request)).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Equal(request.EndDate, chore.EndDate);
        Assert.Equal(request.Interval, chore.Interval);
        Assert.Equal(request.Duration, chore.Duration);
    }
}
