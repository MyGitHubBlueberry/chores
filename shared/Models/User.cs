using System.ComponentModel.DataAnnotations;

public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    public required string Username { get; set; }

    [Required]
    public byte[] Password { get; set; } = Array.Empty<byte>();

    public string? AvatarUrl { get; set; }

    public ICollection<Chore> OwnedChores { get; set; } = new List<Chore>();
    public ICollection<ChoreMember> Memberships { get; set; } = new List<ChoreMember>();
    public ICollection<ChoreLog> Logs { get; set; } = new List<ChoreLog>();
}
