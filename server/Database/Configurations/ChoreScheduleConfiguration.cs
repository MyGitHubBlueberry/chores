namespace Database.Configurations;

using Shared.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ChoreScheduleConfiguration : IEntityTypeConfiguration<ChoreSchedule>
{
    public void Configure(EntityTypeBuilder<ChoreSchedule> builder)
    {
        builder
            .HasKey(cd => cd.ChoreId);
        builder
            .HasOne(cd => cd.Chore)
            .WithOne(c => c.Schedule)
            .HasForeignKey<ChoreSchedule>(cd => cd.ChoreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
