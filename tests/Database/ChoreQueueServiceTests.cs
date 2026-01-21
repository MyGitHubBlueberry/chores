using Database;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;

namespace Tests.Database;

[Trait("Database", "QueueService")]
public class ChoreQueueServiceTests
{
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

        Assert.True((await DbTestHelper.GetChoreQueueService(context)
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
        Assert.True((await DbTestHelper.GetChoreQueueService(context)
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
        Assert.True((await DbTestHelper.GetChoreQueueService(context)
                .ExtendQueueFromDaysAsync(chore.Id, TimeSpan.FromDays(1).Days)).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Single(chore.QueueItems);
        var firstUserUsername = context.Users
            .First(u => u.Id == chore.QueueItems.First().AssignedMemberId).Username;
        Assert.Equal("member1", firstUserUsername);
        Assert.True((await DbTestHelper.GetChoreQueueService(context)
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
        Assert.False((await DbTestHelper.GetChoreQueueService(context)
                .SwapQueueItemsAsync(chore.Id, chore.OwnerId, queueItemAId, queueItemBId)).IsSuccess);
    }

    [Fact]
    public async Task ExtendQueue_Works_With_Entry_Count()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        Chore chore = await new DbTestChoreBuilder(context)
            .WithMember("first", 0)
            .WithMember("second", 1)
            .WithOwner("third", 2)
            .WithDuration(TimeSpan.FromHours(2))
            .WithInterval(TimeSpan.FromMinutes(30))
            .BuildAsync();
        int generatedEntryCount = 5;
        Assert.Empty(chore.QueueItems);
        Assert.True((await DbTestHelper
                    .GetChoreQueueService(context)
                    .ExtendQueueFromEntryCountAsync(chore, generatedEntryCount))
                .IsSuccess);
        Assert.Equal(generatedEntryCount, chore.QueueItems.Count);
        var receivedOrder = chore.QueueItems
            .OrderBy(i => i.ScheduledDate)
            .Select(i => chore.Members
                    .First(m => m.UserId == i.AssignedMemberId).RotationOrder);
        Assert.Equal([0, 1, 2, 0, 1], receivedOrder);
    }

    [Fact]
    public async Task ExtendQueue_Doesnt_Extend_Past_End_Date()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var choreDuration = TimeSpan.FromHours(5);
        Chore chore = await new DbTestChoreBuilder(context)
            .WithMember("first", 0)
            .WithMember("second", 1)
            .WithOwner("third", 2)
            .WithDuration(TimeSpan.FromHours(1))
            .WithEndDate(DateTime.UtcNow + choreDuration)
            .WithStartDate(DateTime.UtcNow)
            .BuildAsync();
        int requestedExtendEntryCount = choreDuration.Hours + 2;
        Assert.Empty(chore.QueueItems);
        Assert.True((await DbTestHelper
                    .GetChoreQueueService(context)
                    .ExtendQueueFromEntryCountAsync(chore, requestedExtendEntryCount))
                .IsSuccess);
        Assert.Equal(choreDuration.Hours, chore.QueueItems.Count);
        var receivedOrder = chore.QueueItems
            .OrderBy(i => i.ScheduledDate)
            .Select(i => chore.Members
                    .First(m => m.UserId == i.AssignedMemberId).RotationOrder);
        Assert.Equal([0, 1, 2, 0, 1], receivedOrder);
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
        var service = DbTestHelper.GetChoreQueueService(context);

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
            Assert.True((await DbTestHelper.GetChoreQueueService(context)
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
        var service = DbTestHelper.GetChoreQueueService(context);
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
        var service = DbTestHelper.GetChoreQueueService(context);
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
        var service = DbTestHelper.GetChoreQueueService(context);
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
        var service = DbTestHelper.GetChoreQueueService(context);
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
        var service = DbTestHelper.GetChoreQueueService(context);
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
        var service = DbTestHelper.GetChoreQueueService(context);
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
        var service = DbTestHelper.GetChoreQueueService(context);
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
        var service = DbTestHelper.GetChoreQueueService(context);
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
    public async Task RegenerateQueue_Not_Generates_Queue_When_Empty()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = DbTestHelper.GetChoreQueueService(context);
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .WithMember("member1", 0)
            .WithMember("member2", 1)
            .WithDuration(TimeSpan.FromDays(1))
            .WithInterval(TimeSpan.FromHours(12))
            .BuildAsync();

        Assert.True((await service.RegenerateQueueAsync(chore.Id, chore.OwnerId)).IsSuccess);
        Assert.Empty(chore.QueueItems);
    }

    [Fact]
    public async Task RegenerateQueue_Does_Nothing_When_No_Members()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = DbTestHelper.GetChoreQueueService(context);
        var chore = await new DbTestChoreBuilder(context)
            .WithOwner()
            .BuildAsync();

        Assert.True((await service.RegenerateQueueAsync(chore.Id, chore.OwnerId)).IsSuccess);
        Assert.Empty(chore.QueueItems);
    }

    //todo: add tests for regenerate queue after
    // swaps, interval change, deletions, insertions
    [Fact]
    public async Task RegenerateQueue_Regenerates_Changed_Queue()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        using var context = new Context(options);
        var service = DbTestHelper.GetChoreQueueService(context);
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
        Assert.True((await service.RegenerateQueueAsync(chore.Id, chore.OwnerId)).IsSuccess);
        Assert.Equal(initialQueueItems
                    .Select(i => i.AssignedMemberId),
                chore.QueueItems
                    .OrderBy(i => i.ScheduledDate)
                    .Take(initialQueueItems.Count())
                    .Select(i => i.AssignedMemberId));
        Assert.Equal(initialQueueItems.Count(), chore.QueueItems.Count);
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
            Assert.False((await DbTestHelper.GetChoreQueueService(context)
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
        Assert.True((await DbTestHelper.GetChoreQueueService(context)
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
        Assert.True((await DbTestHelper.GetChoreQueueService(context)
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
}
