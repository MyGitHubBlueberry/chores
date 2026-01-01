namespace Shared.Models;

using Shared.Enums;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class ChoreLog
{
    [Key]
    public int Id { get; set; }

    public int ChoreId { get; set; }
    public int? UserId { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public ChoreStatus Status { get; set; } = ChoreStatus.Completed;

    [ForeignKey(nameof(ChoreId))]
    public Chore? Chore { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}
