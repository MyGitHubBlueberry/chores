namespace Database;

using Microsoft.EntityFrameworkCore;
using Shared.Models;

public class Context : DbContext
{
    public DbSet<Chore> Chores { get; set; }
    public DbSet<ChoreAdmin> ChoreAdmins { get; set; }
    public DbSet<ChoreDescription> ChoreDescriptions { get; set; }
    public DbSet<ChoreLog> ChoreLogs { get; set; }
    public DbSet<ChoreMember> ChoreMembers { get; set; }
    public DbSet<ChoreQueue> ChoreQueue { get; set; }
    public DbSet<ChoreSchedule> ChoreSchedules { get; set; }
    public DbSet<ChoreState> ChoreStates { get; set; }
    public DbSet<User> Users { get; set; }

    protected override OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);
    }
}
