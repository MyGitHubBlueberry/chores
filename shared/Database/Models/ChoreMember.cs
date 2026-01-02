namespace Shared.Database.Models;

using System.ComponentModel.DataAnnotations.Schema;

public class ChoreMember
{
    public int ChoreId { get; set; }
    public int UserId { get; set; }
    
    // Nullable, so admins aren't forced to do chores
    public int? RotationOrder { get; set; } = 0;

    [ForeignKey(nameof(ChoreId))]
    public Chore? Chore { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public ChoreAdmin? AdminRole { get; set; }
}
