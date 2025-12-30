namespace Database.Configurations;

using Shared.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ChoreDescriptionConfiguration : IEntityTypeConfiguration<ChoreDescription>
{
    public void Configure(EntityTypeBuilder<ChoreDescription> builder)
    {
        builder
            .HasKey(cd => cd.ChoreId);
        builder
            .HasOne(cd => cd.Chore)
            .WithOne(c => c.Description)
            .HasForeignKey<ChoreDescription>(cd => cd.ChoreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
