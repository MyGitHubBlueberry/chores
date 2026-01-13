using Database;
using Shared.Database.Models;

namespace Tests.Database;

public class DbTestChoreBuilder(Context db)
{
    Chore chore = new Chore();
    List<(User, ChoreMember)> members = new();

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

    public DbTestChoreBuilder WithOwner(string name = "owner", int? rotationOrder = null)
    {
        User user = DbTestHelper.CreateUser(name);
        user.OwnedChores.Add(chore);
        members.Add((user, new ChoreMember
        {
            IsAdmin = true,
            RotationOrder = rotationOrder,
        }));
        
        return this;
    }

    public DbTestChoreBuilder WithAdmin(string name = "admin", int? rotationOrder = null)
    {
        members.Add((DbTestHelper.CreateUser(name), new ChoreMember
        {
            IsAdmin = true,
            RotationOrder = rotationOrder,
        }));
        return this;
    }

    public DbTestChoreBuilder WithMember(string name = "member", int? rotationOrder = null)
    {
        members.Add((DbTestHelper.CreateUser(name), new ChoreMember
        {
            IsAdmin = false,
            RotationOrder = rotationOrder,
        }));
        return this;
    }

    public DbTestChoreBuilder WithDuration(TimeSpan duration)
    {
        chore.Duration = duration;
        return this;
    }

    public DbTestChoreBuilder WithInterval(TimeSpan interval)
    {
        chore.Interval = interval;
        return this;
    }

    public async Task<Chore> BuildAsync(CancellationToken token = default)
    {
        if (members.Any()) {
            await db.Users.AddRangeAsync(members.Select(m => m.Item1), token);
            await db.SaveChangesAsync(token);
            foreach (var (user, member) in members) {
                member.UserId = user.Id;
                chore.Members.Add(member);
            }

            chore.CurrentQueueMemberIdx = members
                .Select(pair => pair.Item2.RotationOrder)
                .Where(o => o.HasValue)
                .OrderBy(o => o)
                .FirstOrDefault();
        }
        await db.SaveChangesAsync(token);
        return chore;
    }
}
