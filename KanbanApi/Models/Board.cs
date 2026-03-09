namespace KanbanApi.Models;

public class Board
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public ICollection<BoardMember> Members { get; set; } = [];
    public ICollection<Column> Columns { get; set; } = [];
}
