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
            chore = await new DbTestHelper
                .ChoreBuilder(context)
                .WithOwner().GetAwaiter().GetResult()
                .Build();
            notMember = await DbTestHelper.CreateAndAddUser("user", context);
        }

        using (var context = new Context(options))
        {
            Assert.NotEmpty(context.Chores);

            Assert.False(await new ChoreService(context, CancellationToken.None)
                    .DeleteChoreAsync(notMember.Id, chore.Id));
        }
    }

    [Fact]
    public async Task DeleteChore_Owner_Can_Delete()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore;
        var owner = new User
        {
            Username = "Owns a chore",
            Password = [1],
        };

        using (var context = new Context(options))
        {
            await context.Users.AddAsync(owner);
            await context.SaveChangesAsync();
            chore = new Chore
            {
                OwnerId = owner.Id,
                Title = "Test",
            };
            owner.OwnedChores.Add(chore);
            await context.SaveChangesAsync();
        }

        using (var context = new Context(options))
        {
            Assert.NotEmpty(context.Chores);

            Assert.True(await new ChoreService(context, CancellationToken.None)
                    .DeleteChoreAsync(owner.Id, chore.Id));
        }
    }

    [Fact]
    public async Task DeleteChore_Admin_Can_Delete()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore;
        var owner = new User
        {
            Username = "Owns a chore",
            Password = [1],
        };
        var admin = new User
        {
            Username = "Just an admin",
            Password = [1],
        };

        using (var context = new Context(options))
        {
            await context.Users.AddRangeAsync(owner, admin);
            await context.SaveChangesAsync();
            chore = new Chore
            {
                OwnerId = owner.Id,
                Title = "Test",
            };
            owner.OwnedChores.Add(chore);
            await context.SaveChangesAsync();
            chore.Members.Add(new ChoreMember
                    {
                    UserId = admin.Id,
                    IsAdmin = true,
                    });
            await context.SaveChangesAsync();
        }

        using (var context = new Context(options))
        {
            Assert.NotEmpty(context.Chores);

            var service = new ChoreService(context, CancellationToken.None);
            Assert.True(await service.DeleteChoreAsync(admin.Id, chore.Id));
        }
    }

    [Fact]
    public async Task DeleteChore_Regular_Members_Cant_Delete()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore;
        var owner = new User
        {
            Username = "Owns a chore",
            Password = [1],
        };
        var user = new User
        {
            Username = "Just an admin",
            Password = [1],
        };

        using (var context = new Context(options))
        {
            await context.Users.AddRangeAsync(user, owner);
            await context.SaveChangesAsync();
            chore = new Chore
            {
                Title = "Test",
            };
            chore.Members.Add(new ChoreMember
                    {
                    UserId = user.Id,
                    IsAdmin = false,
                    });
            owner.OwnedChores.Add(chore);
            await context.SaveChangesAsync();
        }

        using (var context = new Context(options))
        {
            Assert.NotEmpty(context.Chores);
            Assert.False(await new ChoreService(context, CancellationToken.None)
                    .DeleteChoreAsync(user.Id, chore.Id));
            Assert.False((await context.ChoreMembers.FirstAsync()).IsAdmin);
        }
    }

    [Fact]
    public async Task UpdateDetails_Updates_Details()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = new Chore
        {
            Title = "Test",
            Body = "Test",
            AvatarUrl = "Test"
        };
        var owner = new User
        {
            Username = "Owns a chore",
            Password = [1],
        };

        using (var context = new Context(options))
        {
            await context.Users.AddAsync(owner);
            owner.OwnedChores.Add(chore);
            await context.SaveChangesAsync();
        }

        using (var context = new Context(options))
        {
            var request = new UpdateChoreDetailsRequest(chore.Id,
                    "new", "new", "new");
            Assert.True(await new ChoreService(context, CancellationToken.None)
                    .UpdateDetailsAsync(owner.Id, request));
            chore = await context.Chores.FirstAsync();
            Assert.Equal(chore.Title, request.Title);
            Assert.Equal(chore.Body, request.Body);
            Assert.Equal(chore.AvatarUrl, request.AvatarUrl);
        }
    }

    [Fact]
    public async Task UpdateDetails_Not_Resets_Properties_If_They_Are_Null()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        Chore chore = new Chore
        {
            Title = "Test",
            Body = "Test",
            AvatarUrl = "Test"
        };
        var owner = new User
        {
            Username = "Owns a chore",
            Password = [1],
        };

        using (var context = new Context(options))
        {
            await context.Users.AddAsync(owner);
            owner.OwnedChores.Add(chore);
            await context.SaveChangesAsync();
        }

        using (var context = new Context(options))
        {
            var request = new UpdateChoreDetailsRequest(chore.Id);
            Assert.True(await new ChoreService(context, CancellationToken.None)
                    .UpdateDetailsAsync(owner.Id, request));
            chore = await context.Chores.FirstAsync();
            Assert.NotNull(chore.Title);
            Assert.NotNull(chore.Body);
            Assert.NotNull(chore.AvatarUrl);
        }
    }

    [Fact]
    public async Task UpdateDetails_Is_Not_For_Regular_Users()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        var startingValue = "Test";
        Chore chore = new Chore
        {
            Title = startingValue,
            Body = startingValue,
            AvatarUrl = startingValue
        };
        var owner = new User
        {
            Username = "Owns a chore",
            Password = [1],
        };
        var user = new User
        {
            Username = "Just a user",
            Password = [1],
        };

        using (var context = new Context(options))
        {
            await context.Users.AddRangeAsync(owner, user);
            owner.OwnedChores.Add(chore);
            await context.SaveChangesAsync();
            chore.Members.Add(new ChoreMember
                    {
                    UserId = user.Id,
                    IsAdmin = false
                    });
            await context.SaveChangesAsync();
        }

        using (var context = new Context(options))
        {
            var request = new UpdateChoreDetailsRequest(chore.Id,
                    "new", "new", "new");
            Assert.False(await new ChoreService(context, CancellationToken.None)
                    .UpdateDetailsAsync(user.Id, request));
            chore = await context.Chores.FirstAsync();
            Assert.Equal(startingValue, chore.Title);
            Assert.Equal(startingValue, chore.Body);
            Assert.Equal(startingValue, chore.AvatarUrl);
        }
    }

    [Fact]
    public async Task UpdateSchedule_Is_Not_For_Regular_Users()
    {
        var (connection, options) = await DbTestHelper.SetupTestDbAsync();
        var startingValue = "Test";
        Chore chore = new Chore
        {
            Title = startingValue,
            Body = startingValue,
            AvatarUrl = startingValue
        };
        var owner = new User
        {
            Username = "Owns a chore",
            Password = [1],
        };
        var user = new User
        {
            Username = "Just a user",
            Password = [1],
        };

        using (var context = new Context(options))
        {
            await context.Users.AddRangeAsync(owner, user);
            owner.OwnedChores.Add(chore);
            await context.SaveChangesAsync();
            chore.Members.Add(new ChoreMember
                    {
                    UserId = user.Id,
                    IsAdmin = false
                    });
            await context.SaveChangesAsync();
        }

        using (var context = new Context(options))
        {
            new ChoreService(context, CancellationToken.None);
        }
    }
}
