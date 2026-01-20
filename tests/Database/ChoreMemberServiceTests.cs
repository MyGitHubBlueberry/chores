using Database;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;
using Shared.Networking.Packets;

namespace Tests.Database;

[Trait("Database", "MemberService")]
public class ChoreMemberServiceTests 
{
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
            var service = DbTestHelper.GetChoreMemberService(context);
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
