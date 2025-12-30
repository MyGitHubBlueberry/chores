namespace Shared.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class ChoreState
{
    [Key]
    public int ChoreId { get; set; }

    [ForeignKey(nameof(ChoreId))]
    public Chore? Chore { get; set; }

    public bool IsPaused { get; set; } = false;
    public int NextMemberIdx { get; set; } = 0;
}
