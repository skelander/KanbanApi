namespace KanbanApi.Models;

public class CardStateHistory
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;
    public int ColumnId { get; set; }
    public Column Column { get; set; } = null!;
    public DateTime EnteredAt { get; set; }
    public DateTime? ExitedAt { get; set; }
}
