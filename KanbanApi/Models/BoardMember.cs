namespace KanbanApi.Models;

public class BoardMember
{
    public int BoardId { get; set; }
    public Board Board { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
