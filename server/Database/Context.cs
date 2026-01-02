namespace Database;

using System;
using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;

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

    public string DbPath { get; }

    public Context() {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = System.IO.Path.Join(path, "chores.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) 
        => optionsBuilder.UseSqlite($"Data Source={DbPath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<ChoreMember>()
            .HasKey(cm => new { cm.ChoreId, cm.UserId });

        modelBuilder.Entity<ChoreAdmin>()
            .HasKey(ca => new { ca.ChoreId, ca.UserId });

        ConfigureOneToManyRelations(modelBuilder);
        ConfigureOneToOneRelations(modelBuilder);
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
