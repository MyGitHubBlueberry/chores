namespace Database.Configurations;

using Shared.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ChoreAdminConfiguration : IEntityTypeConfiguration<ChoreAdmin>
{
    public void Configure(EntityTypeBuilder<ChoreAdmin> builder)
    {
        builder
            .HasKey(a => new { a.ChoreId, a.UserId });
        builder
            .HasOne(a => a.Member)
            .WithOne(m => m.AdminRole)
            .HasForeignKey<ChoreAdmin>(a => new { a.ChoreId, a.UserId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
