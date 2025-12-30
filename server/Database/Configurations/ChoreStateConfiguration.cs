namespace Database.Configurations;

using Shared.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ChoreStateConfiguration : IEntityTypeConfiguration<ChoreState>
{
    public void Configure(EntityTypeBuilder<ChoreState> builder)
    {
        builder
            .HasKey(cd => cd.ChoreId);
        builder
            .HasOne(cd => cd.Chore)
            .WithOne(c => c.State)
            .HasForeignKey<ChoreState>(cd => cd.ChoreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
