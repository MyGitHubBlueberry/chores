using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class ChoreSchedule
{
    [Key]
    public int ChoreId { get; set; }

    [ForeignKey(nameof(ChoreId))]
    public Chore? Chore { get; set; }

    public DateTime StartDate { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan Interval { get; set; } = TimeSpan.Zero;
}
