namespace Database.Configurations;

using Shared.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ChoreConfiguration : IEntityTypeConfiguration<Chore>
{
    public void Configure(EntityTypeBuilder<Chore> builder)
    {
        builder
            .HasOne(c => c.Owner)
            .WithMany(u => u.OwnedChores)
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
