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
                .WithOwner()
                .BuildAsync();
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
                .WithOwner()
                .BuildAsync();

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
        Assert.False(await service.DeleteChoreAsync(adminId, chore.Id));
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
        Assert.False(await new ChoreService(context, CancellationToken.None)
                .DeleteChoreAsync(userId, chore.Id));
        Assert.NotEmpty(context.Chores);
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
                .WithOwner()
                .BuildAsync();

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
                .WithOwner()
                .WithMember()
                .BuildAsync();
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
                .WithOwner()
                .WithMember()
                .BuildAsync();
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
            Assert.True(await service.AddMemberAsync(chore.OwnerId, request));
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
        Assert.True(await service
                .DeleteMemberAsync(chore.Id,
                    chore.OwnerId,
                    chore.Members.First(m => !m.IsAdmin).UserId));
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
        Assert.True(await service
                .DeleteMemberAsync(chore.Id, memberId, memberId));
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
        Assert.True(await service
                .DeleteMemberAsync(chore.Id, chore.OwnerId, chore.OwnerId));
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
        Assert.False(await service
                .DeleteMemberAsync(chore.Id, adminIds[0], adminIds[1]));
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
        Assert.True(await service.SetAdminStatusAsync(chore.Id,
                chore.OwnerId,
                chore.Members.Where(m => !m.IsAdmin)
                    .Select(m => m.UserId)
                    .First(),
                true));
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
        Assert.False(await service
                .SetAdminStatusAsync(chore.Id, adminIds[0], adminIds[1], false));
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
        Assert.False(await service
                .SetAdminStatusAsync(chore.Id, membersIds[0], membersIds[1], false));
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
            Assert.False(await new ChoreService(context, CancellationToken.None)
                    .ExtendQueueAsync(chore.Id, 30));
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
        Assert.True(await new ChoreService(context, CancellationToken.None)
                .ExtendQueueAsync(chore.Id, days));
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
        Assert.True(await new ChoreService(context, CancellationToken.None)
                .ExtendQueueAsync(chore.Id, days));
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

        Assert.True(await new ChoreService(context, CancellationToken.None)
                .ExtendQueueAsync(chore.Id, days));
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
        Assert.True(await new ChoreService(context, CancellationToken.None)
                .ExtendQueueAsync(chore.Id, days));
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
        Assert.True(await new ChoreService(context, CancellationToken.None)
                .ExtendQueueAsync(chore.Id, TimeSpan.FromDays(1).Days));
        chore = await context.Chores.FirstAsync();
        Assert.Single(chore.QueueItems);
        var firstUserUsername = context.Users
            .First(u => u.Id == chore.QueueItems.First().AssignedMemberId).Username;
        Assert.Equal("member1", firstUserUsername);
        Assert.True(await new ChoreService(context, CancellationToken.None)
                .ExtendQueueAsync(chore.Id, TimeSpan.FromDays(1).Days));
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
        Assert.False(await new ChoreService(context, CancellationToken.None)
                .SwapQueueItemsAsync(chore.Id, chore.OwnerId, queueItemAId, queueItemBId));
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

        Assert.True(await service.ExtendQueueAsync(chore.Id, 2));
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
        Assert.True(await service
                .SwapQueueItemsAsync(chore.Id, chore.OwnerId, queueItemIds[0], queueItemIds[1]));
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
            Assert.True(await new ChoreService(context, CancellationToken.None)
                    .SwapMembersInQueueAsync
                    (chore.OwnerId, chore.Id, members[0].UserId, members[1].UserId));
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
        Assert.True(await service.ExtendQueueAsync(chore.Id, 4));
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
        Assert.True(await service
                .SwapQueueItemsAsync(chore.Id, chore.OwnerId, queueItemIds[0], queueItemIds[1]));
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
        Assert.True(await service.InsertQueueEntryAsync(chore.Id, chore.OwnerId,
                new ChoreQueue
                {
                    AssignedMemberId = chore.OwnerId,
                    ScheduledDate = DateTime.UtcNow
                }));
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
        Assert.True(await service.ExtendQueueAsync(chore.Id, 2));
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
        Assert.True(await service.ExtendQueueAsync(chore.Id, days));
        Assert.True(await service.InsertMemberInQueueAsync(chore.Id,
                    chore.OwnerId,
                    users.First(u => u.Username == "member3").Id,
                    lastRotationPosition));
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
        Assert.True(await service.ExtendQueueAsync(chore.Id, days));
        var member2Entries = chore.QueueItems
            .Where(i => users
                .Where(u => u.Id == i.AssignedMemberId
                    && u.Username == "member2").Any());
        Assert.Single(member2Entries);
        Assert.True(await service
            .DeleteQueueEntryAsync(chore.Id, chore.OwnerId, member2Entries.First()));
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
        Assert.True(await service.ExtendQueueAsync(chore.Id, days));
        Assert.Equal(3, chore.QueueItems.Count);
        var timeBetweenChores = chore.Duration + chore.Interval;
        var dates = chore.QueueItems.Select(i => i.ScheduledDate).OrderBy(d => d).Take(2).ToArray();
        Assert.Equal(timeBetweenChores, (dates[0] - dates[1]).Duration());
        Assert.True(await service
            .DeleteQueueEntryAsync(chore.Id, chore.OwnerId,
                chore.QueueItems
                .OrderBy(i => i.ScheduledDate)
                .Skip(1)
                .First()));
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
        Assert.True(await service.ExtendQueueAsync(chore.Id, days));
        var userToBeRemovedExists = chore.QueueItems
            .Where(i => i.AssignedMemberId == userToBeRemoved.Id);
        var usersWithDates = users.Join(chore.QueueItems,
                user => user.Id,
                item => item.AssignedMemberId,
                (user, item) => new { Username = user.Username, Date = item.ScheduledDate });
        Assert.True(userToBeRemovedExists.Any());
        Assert.True(
                await service.DeleteMemberFromQueueAsync(chore.Id,
                    chore.OwnerId,
                    userToBeRemoved.Id));
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
        Assert.True(await service.ExtendQueueAsync(chore.Id, days));
        Assert.True(
                await service.DeleteMemberFromQueueAsync(chore.Id,
                    chore.OwnerId,
                    chore.Members.First(u => u.RotationOrder.HasValue).UserId));

        var orderedQueueItemDates = chore.QueueItems
            .Select(i => i.ScheduledDate)
            .OrderBy(d => d);
        var prev = orderedQueueItemDates.First();
        foreach(var curr in orderedQueueItemDates.Skip(1))
        {
            Assert.Equal(choreOffset, curr - prev);
            prev = curr;
        }
    }
}
