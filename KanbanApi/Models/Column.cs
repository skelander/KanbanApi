namespace KanbanApi.Models;

public class Column
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int Position { get; set; }
    public int? WipLimit { get; set; }
    public int BoardId { get; set; }
    public Board Board { get; set; } = null!;
    public ICollection<Card> Cards { get; set; } = [];
}
