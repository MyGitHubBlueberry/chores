namespace Database;

using System;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;

public class Context : DbContext
{
    public DbSet<Chore> Chores => Set<Chore>();
    public DbSet<ChoreAdmin> ChoreAdmins => Set<ChoreAdmin>();
    public DbSet<ChoreDescription> ChoreDescriptions => Set<ChoreDescription>();
    public DbSet<ChoreLog> ChoreLogs => Set<ChoreLog>();
    public DbSet<ChoreMember> ChoreMembers => Set<ChoreMember>();
    public DbSet<ChoreQueue> ChoreQueue => Set<ChoreQueue>();
    public DbSet<ChoreSchedule> ChoreSchedules => Set<ChoreSchedule>();
    public DbSet<ChoreState> ChoreStates => Set<ChoreState>();
    public DbSet<User> Users => Set<User>();

    public Context(DbContextOptions<Context> options) : base (options) { }
    public Context() { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        if (optionsBuilder.IsConfigured) return;

        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        var dbPath = System.IO.Path.Join(path, "chores.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<ChoreMember>()
            .HasKey(cm => new { cm.ChoreId, cm.UserId });

        modelBuilder.Entity<ChoreAdmin>()
            .HasKey(ca => new { ca.ChoreId, ca.UserId });

        ConfigureOneToManyRelations(modelBuilder);
        ConfigureOneToOneRelations(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private void ConfigureOneToManyRelations(ModelBuilder modelBuilder) {
        modelBuilder.Entity<ChoreQueue>()
            .HasOne(cq => cq.AssignedMember)
            .WithMany()
            .HasForeignKey(cq => new { cq.ChoreId, cq.AssignedMemberId })
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChoreLog>()
            .HasOne(cl => cl.User)
            .WithMany(u => u.Logs)
            .HasForeignKey(cl => cl.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureOneToOneRelations(ModelBuilder modelBuilder) {
        modelBuilder.Entity<ChoreAdmin>()
            .HasOne(ca => ca.Member)
            .WithOne(cm => cm.AdminRole)
            .HasForeignKey<ChoreAdmin>(ca => new { ca.ChoreId, ca.UserId })
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChoreState>()
            .HasOne(x => x.Chore)
            .WithOne(c => c.State)
            .HasForeignKey<ChoreState>(x => x.ChoreId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChoreSchedule>()
            .HasOne(x => x.Chore)
            .WithOne(c => c.Schedule)
            .HasForeignKey<ChoreSchedule>(x => x.ChoreId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChoreDescription>()
            .HasOne(x => x.Chore)
            .WithOne(c => c.Description)
            .HasForeignKey<ChoreDescription>(x => x.ChoreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
