using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class ChoreDescription
{
    [Key]
    public int ChoreId { get; set; }

    [ForeignKey(nameof(ChoreId))]
    public Chore? Chore { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }
    public string? AvatarUrl { get; set; }
}
