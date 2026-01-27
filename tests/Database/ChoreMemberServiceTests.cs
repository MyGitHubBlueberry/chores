using Database;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking;
using Shared.Networking.Packets;

namespace Tests.Database;

[Trait("Database", "MemberService")]
public class ChoreMemberServiceTests
{
    [Fact]
    public async Task AddMembers_Can_Add_One()
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

        MemberStatus stasus = new MemberStatus(IsAdmin: false, RotationOrder: null);
        var dict = new Dictionary<string, MemberStatus>(1);
        dict.Add("user", stasus);
        var request = new AddMembersRequest(chore.Id, dict);

        using (var context = new Context(options))
        {
            var service = DbTestHelper.GetChoreMemberService(context);
            Assert.Single(chore.Members);
            Assert.True((await service.AddMembersAsync(chore.OwnerId, request)).IsSuccess);
            chore = await context.Chores.FirstAsync();
            Assert.Equal(2, chore.Members.Count);
        }
    }

    [Fact]
    public async Task AddMembers_Can_Add_Many()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore;
        string[] usernames = {
            "test",
            "hello world",
            "asldkjdsfjkl",
            "123",
            "name"
        };
        MemberStatus stasus = new MemberStatus(IsAdmin: false, RotationOrder: null);
        var dict = new Dictionary<string, MemberStatus>(1);

        using (var context = new Context(options))
        {
            chore = await new DbTestChoreBuilder(context)
                    .WithOwner()
                    .BuildAsync();
            await context.Users
                .AddRangeAsync(usernames
                        .Select(u =>
                        {
                            dict.Add(u, stasus);
                            return DbTestHelper.CreateUser(u);
                        }));
            await context.SaveChangesAsync();

        }
        var request = new AddMembersRequest(chore.Id, dict);
        var expectedCount = usernames.Length + 1; // + owner

        using (var context = new Context(options))
        {
            Assert.Equal(expectedCount, await context.Users.CountAsync());
            Assert.Single(chore.Members);
            Assert.True((await DbTestHelper.GetChoreMemberService(context)
                .AddMembersAsync(chore.OwnerId, request)).IsSuccess);
        }
        Assert.Equal(expectedCount,
                (await (new Context(options)).ChoreMembers.CountAsync()));
    }

    [Fact]
    public async Task AddMembers_Dont_Add_If_User_Is_Missing()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore;
        User user;
        string fakeUsername = "fakeUsername";
        using (var context = new Context(options))
        {
            chore = await new DbTestChoreBuilder(context)
                .WithOwner().BuildAsync();
            user = await DbTestHelper.CreateAndAddUser("existing user", context);
        }
        using (var context = new Context(options))
        {
            MemberStatus stasus = new MemberStatus(IsAdmin: false, RotationOrder: null);
            var dict = new Dictionary<string, MemberStatus>(1);
            dict.Add(user.Username, stasus);
            dict.Add(fakeUsername, stasus);
            var request = new AddMembersRequest(chore.Id, dict);
            var result = await DbTestHelper.GetChoreMemberService(context)
                .AddMembersAsync(chore.OwnerId, request);
            Assert.False(result.IsSuccess);
            Assert.Equal(ServiceError.NotFound, result.Error);
            Assert.Single(chore.Members); //only owner
        }
    }

    [Fact]
    public async Task AddMembers_Dont_Add_If_User_Is_Member()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore;
        User user;
        string memberUsername = "test";
        using (var context = new Context(options))
        {
            chore = await new DbTestChoreBuilder(context)
                .WithOwner()
                .WithMember(memberUsername)
                .BuildAsync();
            user = await DbTestHelper.CreateAndAddUser("existing user", context);
        }
        using (var context = new Context(options))
        {
            MemberStatus stasus = new MemberStatus(IsAdmin: false, RotationOrder: null);
            var dict = new Dictionary<string, MemberStatus>(1);
            dict.Add(user.Username, stasus);
            dict.Add(memberUsername, stasus);
            int countBefore = chore.Members.Count;
            var request = new AddMembersRequest(chore.Id, dict);
            var result = await DbTestHelper.GetChoreMemberService(context)
                .AddMembersAsync(chore.OwnerId, request);
            Assert.False(result.IsSuccess);
            Assert.Equal(ServiceError.Conflict, result.Error);
            Assert.Equal(countBefore, chore.Members.Count);
        }
    }

    [Fact]
    public async Task AddMembers_Rejects_Request_Without_Members()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                    .WithOwner()
                    .BuildAsync();

        var dict = new Dictionary<string, MemberStatus>();
        var request = new AddMembersRequest(chore.Id, dict);

        using var context = new Context(options);
        var service = DbTestHelper.GetChoreMemberService(context);
        Assert.Single(chore.Members);
        var result = await service.AddMembersAsync(chore.OwnerId, request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceError.InvalidInput, result.Error);
    }

    [Theory]
    [InlineData("ehsllssl")]
    [InlineData("")]
    [InlineData("123")]
    public async Task AddMembers_Rejects_Unexisting_User(string username)
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = await new DbTestChoreBuilder(new Context(options))
                    .WithOwner()
                    .BuildAsync();

        MemberStatus stasus = new MemberStatus(IsAdmin: false, RotationOrder: null);
        var dict = new Dictionary<string, MemberStatus>();
        dict.Add(username, stasus);
        var request = new AddMembersRequest(chore.Id, dict);
        using var context = new Context(options);
        var service = DbTestHelper.GetChoreMemberService(context);
        var result = await service.AddMembersAsync(chore.OwnerId, request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceError.NotFound, result.Error);
        Assert.Single(chore.Members);
    }

    [Fact]
    public async Task AddMembers_Fixes_Rotation_Order()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        MemberStatus stasus = new MemberStatus(IsAdmin: false, RotationOrder: 0);
        var dict = new Dictionary<string, MemberStatus>();
        Chore chore;
        string[] usernames = {
            "test",
            "hello world",
            "asldkjdsfjkl",
            "123",
            "name"
        };
        using (var context = new Context(options))
        {
            chore = await new DbTestChoreBuilder(new Context(options))
                .WithOwner()
                .BuildAsync();
            await context.Users
                .AddRangeAsync(usernames
                        .Select(u =>
                        {
                            dict.Add(u, stasus);
                            return DbTestHelper.CreateUser(u);
                        }));
            await context.SaveChangesAsync();
        }
        using (var context = new Context(options))
        {
            var request = new AddMembersRequest(chore.Id, dict);
            Assert.True((await DbTestHelper.GetChoreMemberService(context)
                .AddMembersAsync(chore.OwnerId, request)).IsSuccess);
        }
        using (var context = new Context(options))
        {
            var expectedIdxes = usernames.Select((_, idx) => idx);
            var resultInxes = context.ChoreMembers
                .Where(m => m.RotationOrder.HasValue)
                .Select(m => m.RotationOrder!.Value)
                .Order();
            Assert.Equal(expectedIdxes.Count(), resultInxes.Count());
            Assert.Equal(expectedIdxes, resultInxes);
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
        var service = DbTestHelper.GetChoreMemberService(context);
        Assert.Equal(2, chore.Members.Count);

        var request = new DeleteMemberRequest(chore.Id,
                chore.Members.First(m => !m.IsAdmin).UserId);
        Assert.True((await service
                .DeleteMemberAsync(chore.OwnerId, request)).IsSuccess);
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
        var service = DbTestHelper.GetChoreMemberService(context);
        Assert.Equal(2, chore.Members.Count);
        var request = new DeleteMemberRequest(chore.Id,
                memberId);
        Assert.True((await service
                .DeleteMemberAsync(memberId, request)).IsSuccess);
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
        var service = DbTestHelper.GetChoreMemberService(context);
        Assert.Equal(2, chore.Members.Count);
        var request = new DeleteMemberRequest(chore.Id,
                chore.OwnerId);
        Assert.True((await service
                .DeleteMemberAsync(chore.OwnerId, request)).IsSuccess);
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
        var service = DbTestHelper.GetChoreMemberService(context);
        Assert.Equal(3, chore.Members.Count);
        var request = new DeleteMemberRequest(chore.Id,
                adminIds[1]);
        Assert.False((await service
                .DeleteMemberAsync(adminIds[0], request)).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Equal(3, chore.Members.Count);
    }

    [Fact]
    public async Task DeleteMember_Adjusts_Rotation_Order()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        string removeMemberName = "removeMe";
        int removeMemberRotationOrder = 1;
        Chore chore = await new DbTestChoreBuilder(new Context(options))
            .WithOwner("owner", 0)
            .WithMember(removeMemberName, removeMemberRotationOrder)
            .WithMember("member", 2)
            .BuildAsync();
        int removeMemberId = chore.Members
            .First(m => m.RotationOrder == removeMemberRotationOrder)
            .UserId;
        using (var context = new Context(options))
        {
            Assert.Equal(3, chore.Members.Count);
            var request = new DeleteMemberRequest(chore.Id,
                    removeMemberId);
            Assert.True((await DbTestHelper.GetChoreMemberService(context)
                .DeleteMemberAsync(chore.OwnerId, request)).IsSuccess);
        }
        using (var context = new Context(options))
        {
            chore = await context.Chores.Include(ch => ch.Members).FirstAsync();
            Assert.Equal(2, chore.Members.Count);
            Assert.Empty(context.ChoreMembers.Where(m => m.UserId == removeMemberId));
            Assert.Equal([0, 1], chore.Members.Select(m => m.RotationOrder).Order());
        }
    }

    [Fact]
    public async Task
        DeleteMember_Sets_Rotation_Order_To_Null_And_Pauses_Chore_If_After_Delete_Queue_Is_Empty()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        string removeMemberName = "removeMe";
        int removeMemberRotationOrder = 0;
        Chore chore = await new DbTestChoreBuilder(new Context(options))
            .WithOwner("owner")
            .WithMember(removeMemberName, removeMemberRotationOrder)
            .BuildAsync();
        int removeMemberId = chore.Members
            .First(m => m.RotationOrder == removeMemberRotationOrder)
            .UserId;
        using (var context = new Context(options))
        {
            Assert.Equal(2, chore.Members.Count);
            var request = new DeleteMemberRequest(chore.Id,
                    removeMemberId);
            Assert.True((await DbTestHelper.GetChoreMemberService(context)
                .DeleteMemberAsync(chore.OwnerId, request)).IsSuccess);
            chore.IsPaused = false;
        }
        using (var context = new Context(options))
        {
            chore = await context.Chores.Include(ch => ch.Members).FirstAsync();
            Assert.Single(chore.Members);
            Assert.Empty(context.ChoreMembers.Where(m => m.UserId == removeMemberId));
            Assert.Null(chore.CurrentQueueMemberIdx);
            Assert.True(chore.IsPaused);
        }
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
        var service = DbTestHelper.GetChoreMemberService(context);
        Assert.Single(chore.Members, m => m.IsAdmin);
        var request = new SetAdminStatusRequest(chore.Id,
                chore.Members.Where(m => !m.IsAdmin)
                    .Select(m => m.UserId)
                    .First(),
                true);
        Assert.True((await service.SetAdminStatusAsync(chore.OwnerId, request))
                .IsSuccess);
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
        var service = DbTestHelper.GetChoreMemberService(context);
        Assert.Equal(3, chore.Members.Where(m => m.IsAdmin).Count());
        var request = new SetAdminStatusRequest(chore.Id,
                adminIds[1],
                false);
        Assert.False((await service
                .SetAdminStatusAsync(adminIds[0], request)).IsSuccess);
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
        var service = DbTestHelper.GetChoreMemberService(context);
        Assert.Single(chore.Members, m => m.IsAdmin);
        var request = new SetAdminStatusRequest(chore.Id,
                membersIds[1],
                false);
        Assert.False((await service
                .SetAdminStatusAsync(membersIds[0], request)).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Single(chore.Members, m => m.IsAdmin);
    }

}
