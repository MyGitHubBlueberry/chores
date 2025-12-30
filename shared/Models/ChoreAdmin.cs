namespace Shared.Models;

public class ChoreAdmin
{
    public int ChoreId { get; set; }
    public int UserId { get; set; }

    public ChoreMember? Member { get; set; }
}
