namespace Shared.Database.Models;

using System.ComponentModel.DataAnnotations;
using Shared.Networking.Packets;
using Shared.Encryption;

public class User
{
    public User(string username, string password)
    {
        Username = username;
        PasswordHash = PasswordHasher.Hash(password);
    }
    public User(RegisterRequest request)
    {
        Username = request.Username;
        PasswordHash = PasswordHasher.Hash(request.Password);
    }

    [Key]
    public int Id { get; set; }

    [Required, MinLength(5), MaxLength(20)]
    public string Username { get; set; }

    [Required]
    public string PasswordHash { get; set; }

    public string? AvatarUrl { get; set; }

    public ICollection<Chore> OwnedChores { get; set; } = new List<Chore>();
    public ICollection<ChoreMember> Memberships { get; set; } = new List<ChoreMember>();
    public ICollection<ChoreLog> Logs { get; set; } = new List<ChoreLog>();
}
