using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class ChoreQueue
{
    [Key]
    public int Id { get; set; }

    public int ChoreId { get; set; }
    public int AssignedMemberId { get; set; }
    public DateTime ScheduledDate { get; set; }

    [ForeignKey(nameof(ChoreId))]
    public Chore? Chore { get; set; }
    
    [ForeignKey(nameof(AssignedMemberId))]
    public ChoreMember? AssignedMember { get; set; } 
}
