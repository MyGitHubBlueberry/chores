using System.Text.Json.Serialization;

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

    [JsonIgnore] public ICollection<ChoreMember> Members { get; set; } = new List<ChoreMember>();
    [JsonIgnore] public ICollection<ChoreQueue> QueueItems { get; set; } = new List<ChoreQueue>();
    [JsonIgnore] public ICollection<ChoreLog> Logs { get; set; } = new List<ChoreLog>();
}

public struct ChoreDto
{
    public int ChoreId { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public string Privilege { get; set; }
    public bool isPaused { get; set; }
    public int? CurrentQueueMemberIdx { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan Interval { get; set; }
}