using Database;
using Shared.Database.Models;

namespace Tests.Database;

public class DbTestChoreBuilder(Context db)
{
    Chore chore = new Chore();

    public DbTestChoreBuilder WithFill(string fill)
    {
        chore.Title = fill;
        chore.AvatarUrl = fill;
        chore.Body = fill;
        return this;
    }

    public DbTestChoreBuilder WithTitle(string title)
    {
        chore.Title = title;
        return this;
    }

    public async Task<DbTestChoreBuilder> WithOwner(string name = "owner")
    {
        User user = await DbTestHelper.CreateAndAddUser(name, db);
        user.OwnedChores.Add(chore);
        chore.Members.Add(new ChoreMember
        {
            UserId = user.Id,
            IsAdmin = true,
        });
        return this;
    }

    public async Task<DbTestChoreBuilder> WithAdmin(string name = "user")
    {
        User user = await DbTestHelper.CreateAndAddUser(name, db);
        chore.Members.Add(new ChoreMember
        {
            UserId = user.Id,
            IsAdmin = true,
        });
        return this;
    }

    public async Task<DbTestChoreBuilder> WithUser(string name = "user")
    {
        User user = await DbTestHelper.CreateAndAddUser(name, db);
        chore.Members.Add(new ChoreMember
        {
            UserId = user.Id,
            IsAdmin = false,
        });
        return this;
    }

    public async Task<Chore> Build()
    {
        Assert.NotNull(chore.Title);
        if (chore.Members.Count != 0)
            await db.SaveChangesAsync();
        return chore;
    }
}
