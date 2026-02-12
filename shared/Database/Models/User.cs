using System.Text.Json.Serialization;

namespace Shared.Database.Models;

using System.ComponentModel.DataAnnotations;

public class User
{
    [Key]
    public int Id { get; set; }

    [Required, MinLength(5), MaxLength(20)]
    public required string Username { get; set; }

    [Required]
    public required string PasswordHash { get; set; }

    public string? AvatarUrl { get; set; }

    [JsonIgnore]
    public ICollection<Chore> OwnedChores { get; set; } = new List<Chore>();
    [JsonIgnore]
    public ICollection<ChoreMember> Memberships { get; set; } = new List<ChoreMember>();
    [JsonIgnore]
    public ICollection<ChoreLog> Logs { get; set; } = new List<ChoreLog>();
}
