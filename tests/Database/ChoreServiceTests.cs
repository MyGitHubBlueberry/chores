using Database;
using Database.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking;
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

        var service = new ChoreService(context, CancellationToken.None);
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

        var service = new ChoreService(context, CancellationToken.None);
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

        var service = new ChoreService(context, CancellationToken.None);
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

        var service = new ChoreService(context, CancellationToken.None);
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

        var service = new ChoreService(context, CancellationToken.None);
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

            var result = await new ChoreService(context, CancellationToken.None)
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

        var result = await new ChoreService(context, CancellationToken.None)
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
        var service = new ChoreService(context, CancellationToken.None);
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
        var result = await new ChoreService(context, CancellationToken.None)
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

        Assert.True((await new ChoreService(context, CancellationToken.None)
            .UpdateDetailsAsync(chore.OwnerId, request)).IsSuccess);
        Result result = await new ChoreService(context, CancellationToken.None)
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
            result = await new ChoreService(context, CancellationToken.None)
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
        var responce = await new ChoreService(context, CancellationToken.None)
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
        var responce = await new ChoreService(context, CancellationToken.None)
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
        var result = await new ChoreService(context, CancellationToken.None)
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
        var responce = await new ChoreService(context, CancellationToken.None)
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
        var responce = await new ChoreService(context, CancellationToken.None)
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
        Assert.True((await new ChoreService(context, CancellationToken.None)
                .UpdateScheduleAsync(chore.OwnerId, request)).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Equal(request.EndDate, chore.EndDate);
        Assert.Equal(request.Interval, chore.Interval);
        Assert.Equal(request.Duration, chore.Duration);
    }

    [Fact]
    public async Task AddMemberAsync_Adds_Member()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore;
        User user;

        using (var context = new Context(options))
        {
            chore = await new DbTestChoreBuilder(context)
                    .WithOwner()
                    .BuildAsync();
            user = await DbTestHelper.CreateAndAddUser("user", context);
        }

        var request = new AddMemberRequest(chore.Id, user.Username);

        using (var context = new Context(options))
        {
            var service = new ChoreService(context, CancellationToken.None);
            Assert.Single(chore.Members);
            Assert.True((await service.AddMemberAsync(chore.OwnerId, request)).IsSuccess);
            chore = await context.Chores.FirstAsync();
            Assert.Equal(2, chore.Members.Count);
        }
    }

    [Fact]
    public async Task DeleteMember_Deletes_Member()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
            .WithOwner()
            .WithMember()
            .BuildAsync();

        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        Assert.Equal(2, chore.Members.Count);
        Assert.True((await service
                .DeleteMemberAsync(chore.Id,
                    chore.OwnerId,
                    chore.Members.First(m => !m.IsAdmin).UserId)).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Single(chore.Members);
    }

    [Fact]
    public async Task DeleteMember_Members_Can_Leave_By_Themselves()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
            .WithOwner()
            .WithMember()
            .BuildAsync();
        int memberId = chore.Members
            .Where(m => !m.IsAdmin)
            .Select(m => m.UserId)
            .First();

        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        Assert.Equal(2, chore.Members.Count);
        Assert.True((await service
                .DeleteMemberAsync(chore.Id, memberId, memberId)).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Single(chore.Members);
    }

    [Fact]
    public async Task DeleteMember_Deletes_Chore_If_Owner_Leaves()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
            .WithOwner()
            .WithMember()
            .BuildAsync();

        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        Assert.Equal(2, chore.Members.Count);
        Assert.True((await service
                .DeleteMemberAsync(chore.Id, chore.OwnerId, chore.OwnerId)).IsSuccess);
        Assert.Null(await context.Chores.FirstOrDefaultAsync());
    }

    [Fact]
    public async Task DeleteMember_Admins_Cant_Delete_Eachother()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
            .WithOwner()
            .WithAdmin("admin1")
            .WithAdmin("admin2")
            .BuildAsync();
        int[] adminIds = chore.Members
            .Where(m =>
                m.UserId != chore.OwnerId
                && m.IsAdmin)
            .Select(m => m.UserId)
            .Take(2)
            .ToArray();

        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        Assert.Equal(3, chore.Members.Count);
        Assert.False((await service
                .DeleteMemberAsync(chore.Id, adminIds[0], adminIds[1])).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Equal(3, chore.Members.Count);
    }

    [Fact]
    public async Task SetAdminStatusAsync_Owner_Can_Promote()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
            .WithOwner()
            .WithMember()
            .BuildAsync();

        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        Assert.Single(chore.Members, m => m.IsAdmin);
        Assert.True((await service.SetAdminStatusAsync(chore.Id,
                chore.OwnerId,
                chore.Members.Where(m => !m.IsAdmin)
                    .Select(m => m.UserId)
                    .First(),
                true)).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Equal(2, chore.Members.Where(m => m.IsAdmin).Count());
    }

    [Fact]
    public async Task SetAdminStatusAsync_Admins_Cant_Demote_Eachother()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
            .WithOwner()
            .WithAdmin("admin1")
            .WithAdmin("admin2")
            .BuildAsync();
        int[] adminIds = chore.Members
            .Where(m =>
                m.UserId != chore.OwnerId
                && m.IsAdmin)
            .Select(m => m.UserId)
            .Take(2)
            .ToArray();

        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        Assert.Equal(3, chore.Members.Where(m => m.IsAdmin).Count());
        Assert.False((await service
                .SetAdminStatusAsync(chore.Id, adminIds[0], adminIds[1], false)).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Equal(3, chore.Members.Where(m => m.IsAdmin).Count());
    }

    [Fact]
    public async Task SetAdminStatusAsync_Regular_Members_Cant_Promote_Eachother()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
            .WithOwner()
            .WithMember("member1")
            .WithMember("member2")
            .BuildAsync();
        int[] membersIds = chore.Members
            .Where(m => !m.IsAdmin)
            .Select(m => m.UserId)
            .Take(2)
            .ToArray();

        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        Assert.Single(chore.Members, m => m.IsAdmin);
        Assert.False((await service
                .SetAdminStatusAsync(chore.Id, membersIds[0], membersIds[1], false)).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Single(chore.Members, m => m.IsAdmin);
    }

    [Fact]
    public async Task ExtendQueue_Doesnt_Work_Without_Active_Members()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
            .WithOwner()
            .BuildAsync();

        using (var context = new Context(options))
        {
            Assert.False((await new ChoreService(context, CancellationToken.None)
                    .ExtendQueueFromDaysAsync(chore.Id, 30)).IsSuccess);
        }
        using (var context = new Context(options))
        {
            Assert.Empty(context.ChoreQueue);
        }
    }

    [Fact]
    public async Task ExtendQueue_Generates_Right_Amount_Of_Chores_Per_Day()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();

        using var context = new Context(options);
        Chore chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithDuration(TimeSpan.FromDays(1))
            .WithInterval(TimeSpan.Zero)
            .WithMember("member1", 0)
            .WithMember("member2", 1)
            .BuildAsync();
        int days = 3;

        Assert.Empty(chore.QueueItems);
        Assert.True((await new ChoreService(context, CancellationToken.None)
                .ExtendQueueFromDaysAsync(chore.Id, days)).IsSuccess);
        chore = await context.Chores.Include(ch => ch.Members).FirstAsync();
        Assert.NotEmpty(chore.QueueItems);
        Assert.Equal(days, chore.QueueItems.Count);
    }

    [Fact]
    public async Task
        ExtendQueue_Generates_Correct_QueueItem_Amount_And_Distributes_Them_Evenly()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();

        using var context = new Context(options);
        int days = 5;
        TimeSpan duration = TimeSpan.FromHours(12);
        int expected = days * (TimeSpan.HoursPerDay / duration.Hours);
        Chore chore = await new DbTestChoreBuilder(context)
                .WithOwner()
                .WithDuration(duration)
                .WithInterval(TimeSpan.Zero)
                .WithMember("member1", 0)
                .WithMember("member2", 1)
                .BuildAsync();

        Assert.Empty(chore.QueueItems);
        Assert.True((await new ChoreService(context, CancellationToken.None)
                .ExtendQueueFromDaysAsync(chore.Id, days)).IsSuccess);
        chore = await context.Chores.Include(ch => ch.Members).FirstAsync();
        Assert.NotEmpty(chore.QueueItems);
        Assert.Equal(expected, chore.QueueItems.Count);
        var members = chore.Members
        .Where(m => !m.IsAdmin)
        .Take(2)
        .ToArray();
        var member1Count = chore.QueueItems
            .Where(q => q.AssignedMemberId == members[0].UserId)
            .Count();
        var member2Count = chore.QueueItems
            .Where(q => q.AssignedMemberId == members[1].UserId)
            .Count();
        Assert.Equal(member1Count, member2Count);
    }

    [Fact]
    public async Task
        ExtendQueue_Includes_Chore_Without_Interval_At_The_End()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();

        TimeSpan duration = TimeSpan.FromDays(1);
        TimeSpan interval = TimeSpan.FromHours(12);
        int days = 4;

        using var context = new Context(options);
        Chore chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithDuration(duration)
            .WithInterval(interval)
            .WithMember("member1", 0)
            .WithMember("member2", 1)
            .BuildAsync();

        Assert.True((await new ChoreService(context, CancellationToken.None)
                .ExtendQueueFromDaysAsync(chore.Id, days)).IsSuccess);
        Assert.Equal(3, chore.QueueItems.Count);
    }

    [Fact]
    public async Task
        ExtendQueue_Accounts_For_Interval()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();

        int days = 5;
        TimeSpan duration = TimeSpan.FromHours(12);
        TimeSpan interval = TimeSpan.FromHours(12);
        int expected = days * TimeSpan.HoursPerDay / (duration.Hours + interval.Hours);

        using var context = new Context(options);
        Chore chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithDuration(duration)
            .WithInterval(interval)
            .WithMember("member1", 0)
            .WithMember("member2", 1)
            .BuildAsync();

        Assert.Empty(chore.QueueItems);
        Assert.True((await new ChoreService(context, CancellationToken.None)
                .ExtendQueueFromDaysAsync(chore.Id, days)).IsSuccess);
        chore = await context.Chores.Include(ch => ch.Members).FirstAsync();
        Assert.NotEmpty(chore.QueueItems);
        Assert.Equal(expected, chore.QueueItems.Count);
    }

    [Fact]
    public async Task
        ExtendQueue_Preserves_Queue_And_Order_When_Called_Twise()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();

        using var context = new Context(options);
        Chore chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithDuration(TimeSpan.FromDays(1))
            .WithMember("member1", 0)
            .WithMember("member2", 1)
            .WithMember("member3", 2)
            .BuildAsync();

        Assert.Empty(chore.QueueItems);
        Assert.True((await new ChoreService(context, CancellationToken.None)
                .ExtendQueueFromDaysAsync(chore.Id, TimeSpan.FromDays(1).Days)).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Single(chore.QueueItems);
        var firstUserUsername = context.Users
            .First(u => u.Id == chore.QueueItems.First().AssignedMemberId).Username;
        Assert.Equal("member1", firstUserUsername);
        Assert.True((await new ChoreService(context, CancellationToken.None)
                .ExtendQueueFromDaysAsync(chore.Id, TimeSpan.FromDays(1).Days)).IsSuccess);
        Assert.Equal(2, chore.QueueItems.Count);
        var items = chore.QueueItems.Take(2).ToArray();
        Assert.NotEqual(items[0].AssignedMemberId, items[1].AssignedMemberId);
        Assert.NotEqual(items[0].ScheduledDate, items[1].ScheduledDate);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 4)]
    public async Task SwapQueueItems_Cant_Swap_Nonexisting_Entries
        (int queueItemAId, int queueItemBId)
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);

        Chore chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .BuildAsync();
        Assert.False((await new ChoreService(context, CancellationToken.None)
                .SwapQueueItemsAsync(chore.Id, chore.OwnerId, queueItemAId, queueItemBId)).IsSuccess);
    }

    [Fact]
    public async Task SwapQueueItems_Swaps_Dates()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        string user1 = "user1";
        string user2 = "user2";

        Chore chore = await new DbTestChoreBuilder(context)
                .WithOwner()
                .WithDuration(TimeSpan.FromDays(1))
                .WithMember(user1, 0)
                .WithMember(user2, 1)
                .BuildAsync();
        var service = new ChoreService(context, CancellationToken.None);

        Assert.True((await service.ExtendQueueFromDaysAsync(chore.Id, 2)).IsSuccess);
        Assert.True(chore.QueueItems.Count == 2);
        var users = context.Users.ToArray();
        var orderedUsers = users
            .Join(chore.QueueItems,
                    user => user.Id,
                    q => q.AssignedMemberId,
                    (user, queue) => new { Name = user.Username, Date = queue.ScheduledDate })
            .OrderBy(x => x.Date)
            .Take(2)
            .Select(x => x.Name)
            .ToArray();

        Assert.Equal(user1, orderedUsers[0]);
        Assert.Equal(user2, orderedUsers[1]);
        var queueItemIds = chore.QueueItems.Take(2).Select(q => q.Id).ToArray();
        Assert.True((await service
                .SwapQueueItemsAsync(chore.Id, chore.OwnerId, queueItemIds[0], queueItemIds[1])).IsSuccess);
        orderedUsers = users
            .Join(chore.QueueItems,
                    user => user.Id,
                    q => q.AssignedMemberId,
                    (user, queue) => new { Name = user.Username, Date = queue.ScheduledDate })
            .OrderBy(x => x.Date)
            .Take(2)
            .Select(x => x.Name)
            .ToArray();
        Assert.Equal(user1, orderedUsers[1]);
        Assert.Equal(user2, orderedUsers[0]);
    }

    [Fact]
    public async Task SwapMembersInQueue_Swaps_Only_RotationOrder_If_Queue_Is_Empty()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore;
        ChoreMember[] members;
        using (var context = new Context(options))
        {
            chore = await new DbTestChoreBuilder(context)
                .WithOwner()
                .WithDuration(TimeSpan.FromDays(1))
                .WithMember("member1")
                .WithMember("member2")
                .BuildAsync();
            members = chore.Members
                .Where(m => !m.IsAdmin)
                .Take(2)
                .ToArray();
            for (int i = 0; i < members.Length; i++)
            {
                members[i].RotationOrder = i;
            }
            chore.CurrentQueueMemberIdx = 0;
            await context.SaveChangesAsync();
        }

        using (var context = new Context(options))
        {
            Assert.Empty(chore.QueueItems);
            Assert.Equal(members.Length, chore.Members
                    .Where(m => m.RotationOrder.HasValue).Count());
            Assert.True((await new ChoreService(context, CancellationToken.None)
                    .SwapMembersInQueueAsync
                    (chore.OwnerId, chore.Id, members[0].UserId, members[1].UserId)).IsSuccess);
        }

        using (var context = new Context(options))
        {
            chore = await context.Chores.Include(ch => ch.Members).FirstAsync();
            Assert.Empty(chore.QueueItems);
            var membersAfterSwap = chore.Members
                .Where(m => m.RotationOrder.HasValue)
                .OrderBy(m => m.RotationOrder)
                .ToArray();
            Assert.Equal(membersAfterSwap.Length, members.Length);
            Assert.True(membersAfterSwap[0].UserId != members[0].UserId
                    || membersAfterSwap[0].RotationOrder != members[0].RotationOrder);
            Assert.True(membersAfterSwap[1].UserId != members[1].UserId
                    || membersAfterSwap[1].RotationOrder != members[1].RotationOrder);
        }
    }

    [Fact]
    public async Task SwapMembersInQueue_Swaps_Entries_InQueue()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var member1 = "member1";
        var member2 = "member2";
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithMember(member1, 0)
            .WithMember(member2, 1)
            .WithDuration(TimeSpan.FromDays(1))
            .BuildAsync();
        var service = new ChoreService(context, CancellationToken.None);
        var users = context.Users.ToArray();
        Assert.True((await service.ExtendQueueFromDaysAsync(chore.Id, 4)).IsSuccess);
        var orderedNames = chore.QueueItems
            .Join(users,
                    q => q.AssignedMemberId,
                    u => u.Id,
                    (q, u) => new { Username = u.Username, Date = q.ScheduledDate })
            .OrderBy(entry => entry.Date)
            .Select(entry => entry.Username)
            .ToArray();
        Assert.Equivalent(new string[] { member1, member2, member1, member2 }, orderedNames);
        var queueItemIds = chore.QueueItems.Take(2).Select(q => q.Id).ToArray();
        Assert.True((await service
                .SwapQueueItemsAsync(chore.Id, chore.OwnerId, queueItemIds[0], queueItemIds[1])).IsSuccess);
        orderedNames = chore.QueueItems
            .Join(users,
                    q => q.AssignedMemberId,
                    u => u.Id,
                    (q, u) => new { Username = u.Username, Date = q.ScheduledDate })
            .OrderBy(entry => entry.Date)
            .Select(entry => entry.Username)
            .ToArray();
        Assert.Equivalent(new string[] { member2, member1, member2, member1 }, orderedNames);
    }

    [Fact]
    public async Task InsertQueueEntry_Can_Insert_In_Empty_Queue()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithDuration(TimeSpan.FromDays(1))
            .BuildAsync();
        Assert.Empty(chore.QueueItems);
        Assert.True((await service.InsertQueueEntryAsync(chore.Id, chore.OwnerId,
                new ChoreQueue
                {
                    AssignedMemberId = chore.OwnerId,
                    ScheduledDate = DateTime.UtcNow
                })).IsSuccess);
        Assert.Single(chore.QueueItems);
        Assert.Equal(chore.OwnerId, chore.QueueItems.First().AssignedMemberId);
    }

    [Fact]
    public async Task InsertQueueEntry_Can_Insert_In_Between_Queue_Entires()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithMember("member1", 0)
            .WithMember("member2", 1)
            .WithMember("member3")
            .WithDuration(TimeSpan.FromDays(1))
            .BuildAsync();
        var newMemberId = chore.Members.Where(m =>
                !m.RotationOrder.HasValue
                && m.UserId != chore.OwnerId).First().UserId;
        Assert.Empty(chore.QueueItems);
        Assert.True((await service.ExtendQueueFromDaysAsync(chore.Id, 2)).IsSuccess);
        Assert.NotEmpty(chore.QueueItems);
        var newMemberScheduledDate = chore.QueueItems
            .Select(i => i.ScheduledDate)
            .OrderBy(d => d)
            .First();
        var queueItem = new ChoreQueue
        {
            AssignedMemberId = newMemberId,
            ScheduledDate = newMemberScheduledDate,
        };
        await service.InsertQueueEntryAsync(chore.Id, chore.OwnerId, queueItem);
        Assert.Contains(queueItem, chore.QueueItems);
        var users = context.Users.Select(u => new { u.Username, u.Id });
        var orderedNames = chore.QueueItems
            .Join(users,
                    q => q.AssignedMemberId,
                    u => u.Id,
                    (q, u) => new { Username = u.Username, Date = q.ScheduledDate })
            .OrderBy(entry => entry.Date)
            .Select(entry => entry.Username)
            .ToArray();
        Assert.Equivalent(new string[] { "member3", "member1", "member2" }, orderedNames);
    }

    [Fact]
    public async Task InsertQueueMember_Inserts_ChoreMember_In_Queue()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithMember("member1", 0)
            .WithMember("member2", 1)
            .WithMember("member3")
            .WithDuration(TimeSpan.FromDays(1))
            .BuildAsync();
        var users = context.Users.Select(u => new { u.Username, u.Id });
        var days = 4;
        var lastRotationPosition = chore.Members.Where(m => m.RotationOrder.HasValue).Count();
        Assert.True((await service.ExtendQueueFromDaysAsync(chore.Id, days)).IsSuccess);
        Assert.True((await service.InsertMemberInQueueAsync(chore.Id,
                    chore.OwnerId,
                    users.First(u => u.Username == "member3").Id,
                    lastRotationPosition)).IsSuccess);
        var orderedNames = chore.QueueItems
            .Join(users,
                    q => q.AssignedMemberId,
                    u => u.Id,
                    (q, u) => new { Username = u.Username, Date = q.ScheduledDate })
            .OrderBy(entry => entry.Date)
            .Select(entry => entry.Username)
            .ToArray();

        Assert.Equivalent(
                new string[] { "member1", "member2", "member3", "member1", "member2", "member3" },
                orderedNames);
    }

    [Fact]
    public async Task DeleteQueueEntry_Removes_Entry()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithMember("member1", 0)
            .WithMember("member2", 1)
            .WithDuration(TimeSpan.FromDays(1))
            .BuildAsync();
        var users = context.Users.Select(u => new { u.Username, u.Id });
        var days = 2;
        Assert.True((await service.ExtendQueueFromDaysAsync(chore.Id, days)).IsSuccess);
        var member2Entries = chore.QueueItems
            .Where(i => users
                .Where(u => u.Id == i.AssignedMemberId
                    && u.Username == "member2").Any());
        Assert.Single(member2Entries);
        Assert.True((await service
            .DeleteQueueEntryAsync(chore.Id, chore.OwnerId, member2Entries.First())).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Empty(member2Entries);
    }

    [Fact]
    public async Task DeleteQueueEntry_Preserves_Interval()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithMember("member1", 0)
            .WithMember("member2", 1)
            .WithDuration(TimeSpan.FromDays(1))
            .WithInterval(TimeSpan.FromHours(12))
            .BuildAsync();
        var users = context.Users.Select(u => new { u.Username, u.Id });
        var days = 4;
        Assert.True((await service.ExtendQueueFromDaysAsync(chore.Id, days)).IsSuccess);
        Assert.Equal(3, chore.QueueItems.Count);
        var timeBetweenChores = chore.Duration + chore.Interval;
        var dates = chore.QueueItems.Select(i => i.ScheduledDate).OrderBy(d => d).Take(2).ToArray();
        Assert.Equal(timeBetweenChores, (dates[0] - dates[1]).Duration());
        Assert.True((await service
            .DeleteQueueEntryAsync(chore.Id, chore.OwnerId,
                chore.QueueItems
                .OrderBy(i => i.ScheduledDate)
                .Skip(1)
                .First())).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Equal(2, chore.QueueItems.Count);
        dates = chore.QueueItems.Select(i => i.ScheduledDate).ToArray();
        Assert.Equal(timeBetweenChores, (dates[0] - dates[1]).Duration());
    }

    [Fact]
    public async Task DeleteMemberFromQueue_Deletes_Member()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        var userToBeRemovedUsername = "member2";
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithMember("member1", 0)
            .WithMember(userToBeRemovedUsername, 1)
            .WithDuration(TimeSpan.FromDays(1))
            .WithInterval(TimeSpan.FromHours(12))
            .BuildAsync();
        var users = context.Users.Select(u => new { u.Username, u.Id });
        var userToBeRemoved = users.Where(u => u.Username == userToBeRemovedUsername).First();
        var days = 5;
        Assert.True((await service.ExtendQueueFromDaysAsync(chore.Id, days)).IsSuccess);
        var userToBeRemovedExists = chore.QueueItems
            .Where(i => i.AssignedMemberId == userToBeRemoved.Id);
        var usersWithDates = users.Join(chore.QueueItems,
                user => user.Id,
                item => item.AssignedMemberId,
                (user, item) => new { Username = user.Username, Date = item.ScheduledDate });
        Assert.True(userToBeRemovedExists.Any());
        Assert.True(
                (await service.DeleteMemberFromQueueAsync(chore.Id,
                    chore.OwnerId,
                    userToBeRemoved.Id)).IsSuccess);
        Assert.False(userToBeRemovedExists.Any());
    }

    [Fact]
    public async Task DeleteMemberFromQueue_Preserves_Intervals()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        var duration = TimeSpan.FromDays(1);
        var interval = TimeSpan.FromHours(12);
        var choreOffset = duration + interval;
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithMember("member1", 0)
            .WithMember("member2", 1)
            .WithDuration(duration)
            .WithInterval(interval)
            .BuildAsync();
        var users = context.Users.Select(u => new { u.Username, u.Id });
        var days = 10;
        Assert.True((await service.ExtendQueueFromDaysAsync(chore.Id, days)).IsSuccess);
        Assert.True(
                (await service.DeleteMemberFromQueueAsync(chore.Id,
                    chore.OwnerId,
                    chore.Members.First(u => u.RotationOrder.HasValue).UserId)).IsSuccess);

        var orderedQueueItemDates = chore.QueueItems
            .Select(i => i.ScheduledDate)
            .OrderBy(d => d);
        var prev = orderedQueueItemDates.First();
        foreach (var curr in orderedQueueItemDates.Skip(1))
        {
            Assert.Equal(choreOffset, curr - prev);
            prev = curr;
        }
    }


    [Fact]
    public async Task RegenerateQueue_Generates_Queue_When_Empty()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithMember("member1", 0)
            .WithMember("member2", 1)
            .WithDuration(TimeSpan.FromDays(1))
            .WithInterval(TimeSpan.FromHours(12))
            .BuildAsync();

        Assert.True((await service.RegenerateQueueAsync(chore.Id, chore.OwnerId)).IsSuccess);
        Assert.NotEmpty(chore.QueueItems);
    }

    [Fact]
    public async Task RegenerateQueue_Does_Nothing_When_No_Members()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .BuildAsync();

        Assert.False((await service.RegenerateQueueAsync(chore.Id, chore.OwnerId)).IsSuccess);
        Assert.Empty(chore.QueueItems);
    }

    //todo: add tests for regenerate queue after
    // swaps, interval change, deletions, insertions
    [Fact]
    public async Task RegenerateQueue_Regenerates_Changed_Queue()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = new ChoreService(context, CancellationToken.None);
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithMember("memeber1", 0)
            .WithMember("memeber2", 1)
            .WithMember("memeber3", 2)
            .WithDuration(TimeSpan.FromDays(1))
            .WithInterval(TimeSpan.FromDays(0))
            .BuildAsync();
        Assert.True((await service.ExtendQueueFromDaysAsync(chore.Id, 10)).IsSuccess);
        var initialQueueItems = chore.QueueItems.OrderBy(i => i.ScheduledDate).ToArray();
        Assert.True((await service.SwapQueueItemsAsync(chore.Id,
                    chore.OwnerId,
                    chore.QueueItems.First().Id,
                    chore.QueueItems.Skip(1).First().Id)).IsSuccess);
        Assert.True((await service.DeleteQueueEntryAsync(chore.Id,
                    chore.OwnerId,
                    chore.QueueItems.Last())).IsSuccess);
        Assert.True((await service.InsertQueueEntryAsync(chore.Id,
                    chore.OwnerId,
                    new ChoreQueue
                    {
                        AssignedMemberId = chore.Members
                            .Where(m => m.RotationOrder.HasValue)
                            .Last().UserId,
                        ScheduledDate = chore.StartDate + chore.Interval,
                    })).IsSuccess);
        Assert.NotEqual(initialQueueItems, chore.QueueItems.OrderBy(i => i.ScheduledDate));
        Assert.True((await service.RegenerateQueueAsync(chore.Id, chore.OwnerId, initialQueueItems.Count())).IsSuccess);
        Assert.Equal(initialQueueItems
                    .Select(i => i.AssignedMemberId),
                chore.QueueItems
                    .OrderBy(i => i.ScheduledDate)
                    .Take(initialQueueItems.Count())
                    .Select(i => i.AssignedMemberId));
        Assert.Equal(initialQueueItems.Count(), chore.QueueItems.Count);
    }
}
