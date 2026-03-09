namespace KanbanApi.Models;

public class Card
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public int Position { get; set; }
    public int ColumnId { get; set; }
    public Column Column { get; set; } = null!;
    public ICollection<CardStateHistory> StateHistory { get; set; } = [];
}
