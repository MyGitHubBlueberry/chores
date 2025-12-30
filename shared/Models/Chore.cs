namespace Shared.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Chore
{
    [Key]
    public int Id { get; set; }

    public int OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public User? Owner { get; set; }

    public ChoreState? State { get; set; }
    public ChoreSchedule? Schedule { get; set; }
    public ChoreDescription? Description { get; set; }

    public ICollection<ChoreMember> Members { get; set; } = new List<ChoreMember>();
    public ICollection<ChoreQueue> QueueItems { get; set; } = new List<ChoreQueue>();
    public ICollection<ChoreLog> Logs { get; set; } = new List<ChoreLog>();
}
