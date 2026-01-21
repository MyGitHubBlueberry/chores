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
        var service = DbTestHelper.GetChoreMemberService(context);
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
        var service = DbTestHelper.GetChoreMemberService(context);
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
        var service = DbTestHelper.GetChoreMemberService(context);
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
        var service = DbTestHelper.GetChoreMemberService(context);
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
        var service = DbTestHelper.GetChoreMemberService(context);
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
        var service = DbTestHelper.GetChoreMemberService(context);
        Assert.Single(chore.Members, m => m.IsAdmin);
        Assert.False((await service
                .SetAdminStatusAsync(chore.Id, membersIds[0], membersIds[1], false)).IsSuccess);
        chore = await context.Chores.FirstAsync();
        Assert.Single(chore.Members, m => m.IsAdmin);
    }

}
