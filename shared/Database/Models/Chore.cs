namespace Shared.Database.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Chore
{
    [Key]
    public int Id { get; set; }

    public int OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public User? Owner { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? AvatarUrl { get; set; }

    public bool IsPaused { get; set; } = true;
    public int? CurrentQueueMemberIdx { get; set; }

    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; } = null;
    public TimeSpan Duration { get; set; } = TimeSpan.FromDays(1);
    public TimeSpan Interval { get; set; } = TimeSpan.Zero;

    public ICollection<ChoreMember> Members { get; set; } = new List<ChoreMember>();
    public ICollection<ChoreQueue> QueueItems { get; set; } = new List<ChoreQueue>();
    public ICollection<ChoreLog> Logs { get; set; } = new List<ChoreLog>();
}
