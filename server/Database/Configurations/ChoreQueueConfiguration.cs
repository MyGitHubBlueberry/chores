namespace Database.Configurations;

using Shared.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ChoreQueueConfiguration : IEntityTypeConfiguration<ChoreQueue>
{
    public void Configure(EntityTypeBuilder<ChoreQueue> builder)
    {
        builder
            .HasKey(cq => cq.Id);
        builder
            .HasOne(cq => cq.AssignedMember)
            .WithMany()
            .HasForeignKey(cq => new { cq.ChoreId, cq.AssignedMemberId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
