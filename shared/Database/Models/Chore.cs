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

    public bool IsPaused { get; set; } = false;
    public int NextMemberIdx { get; set; } = 0;

    public DateTime StartDate { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan Interval { get; set; } = TimeSpan.Zero;

    public ICollection<ChoreMember> Members { get; set; } = new List<ChoreMember>();
    public ICollection<ChoreQueue> QueueItems { get; set; } = new List<ChoreQueue>();
    public ICollection<ChoreLog> Logs { get; set; } = new List<ChoreLog>();
}
