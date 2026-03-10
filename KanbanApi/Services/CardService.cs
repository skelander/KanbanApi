using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KanbanApi.Services;

public class CardService(AppDbContext db, ILogger<CardService> logger) : ICardService
{
    public async Task<ServiceResult<IEnumerable<CardResponse>>> GetCardsAsync(int boardId, int columnId, int userId, bool isAdmin = false)
    {
        if (!isAdmin && !await IsBoardMemberAsync(boardId, userId))
            return await BoardExistsAsync(boardId)
                ? ServiceResult<IEnumerable<CardResponse>>.Forbidden()
                : ServiceResult<IEnumerable<CardResponse>>.NotFound();

        var column = await db.Columns
            .Include(c => c.Cards).ThenInclude(c => c.StateHistory)
            .FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId);

        if (column is null) return ServiceResult<IEnumerable<CardResponse>>.NotFound();

        var cards = column.Cards
            .OrderBy(c => c.Position)
            .Select(MapToResponse);

        return ServiceResult<IEnumerable<CardResponse>>.Ok(cards);
    }

    public async Task<ServiceResult<CardResponse>> CreateCardAsync(int boardId, int columnId, CreateCardRequest request, int userId, bool isAdmin = false)
    {
        if (!isAdmin && !await IsBoardMemberAsync(boardId, userId))
            return await BoardExistsAsync(boardId)
                ? ServiceResult<CardResponse>.Forbidden()
                : ServiceResult<CardResponse>.NotFound();

        var column = await db.Columns
            .Include(c => c.Cards)
            .FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId);

        if (column is null) return ServiceResult<CardResponse>.NotFound();

        var position = column.Cards.Count > 0 ? column.Cards.Max(c => c.Position) + 1 : 0;
        var card = new Card
        {
            Title = request.Title,
            Description = request.Description,
            Position = position,
            ColumnId = columnId
        };

        card.StateHistory.Add(new CardStateHistory
        {
            ColumnId = columnId,
            ColumnName = column.Name,
            EnteredAt = DateTime.UtcNow
        });

        db.Cards.Add(card);
        await db.SaveChangesAsync();

        logger.LogInformation("Created card {CardTitle} in column {ColumnId}", card.Title, columnId);
        return ServiceResult<CardResponse>.Ok(MapToResponse(card));
    }

    public async Task<ServiceResult<CardResponse>> UpdateCardAsync(int boardId, int columnId, int cardId, UpdateCardRequest request, int userId, bool isAdmin = false)
    {
        if (!isAdmin && !await IsBoardMemberAsync(boardId, userId))
            return await BoardExistsAsync(boardId)
                ? ServiceResult<CardResponse>.Forbidden()
                : ServiceResult<CardResponse>.NotFound();

        var card = await db.Cards
            .Include(c => c.StateHistory)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ColumnId == columnId && c.Column.BoardId == boardId);

        if (card is null) return ServiceResult<CardResponse>.NotFound();

        if (request.Title is not null) card.Title = request.Title;
        if (request.Description is not null) card.Description = request.Description;
        if (request.Position.HasValue) card.Position = request.Position.Value;

        await db.SaveChangesAsync();
        return ServiceResult<CardResponse>.Ok(MapToResponse(card));
    }

    public async Task<ServiceResult<bool>> DeleteCardAsync(int boardId, int columnId, int cardId, int userId, bool isAdmin = false)
    {
        if (!isAdmin && !await IsBoardMemberAsync(boardId, userId))
            return await BoardExistsAsync(boardId)
                ? ServiceResult<bool>.Forbidden()
                : ServiceResult<bool>.NotFound();

        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ColumnId == columnId && c.Column.BoardId == boardId);

        if (card is null) return ServiceResult<bool>.NotFound();

        db.Cards.Remove(card);
        await db.SaveChangesAsync();
        logger.LogInformation("Deleted card {CardId}", cardId);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<CardResponse>> MoveCardAsync(int boardId, int columnId, int cardId, MoveCardRequest request, int userId, bool isAdmin = false)
    {
        if (!isAdmin && !await IsBoardMemberAsync(boardId, userId))
            return await BoardExistsAsync(boardId)
                ? ServiceResult<CardResponse>.Forbidden()
                : ServiceResult<CardResponse>.NotFound();

        var card = await db.Cards
            .Include(c => c.StateHistory)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ColumnId == columnId && c.Column.BoardId == boardId);

        if (card is null) return ServiceResult<CardResponse>.NotFound();

        var targetColumn = await db.Columns.FirstOrDefaultAsync(c => c.Id == request.TargetColumnId && c.BoardId == boardId);
        if (targetColumn is null) return ServiceResult<CardResponse>.NotFound();

        var now = DateTime.UtcNow;

        var openState = card.StateHistory.FirstOrDefault(h => h.ExitedAt is null);
        if (openState is not null) openState.ExitedAt = now;

        db.CardStateHistories.Add(new CardStateHistory
        {
            CardId = card.Id,
            ColumnId = request.TargetColumnId,
            ColumnName = targetColumn.Name,
            EnteredAt = now
        });

        card.ColumnId = request.TargetColumnId;
        card.Position = request.Position;

        await db.SaveChangesAsync();
        logger.LogInformation("Moved card {CardId} to column {TargetColumnId}", cardId, request.TargetColumnId);
        return ServiceResult<CardResponse>.Ok(await LoadCardResponseAsync(card.Id));
    }

    private async Task<CardResponse> LoadCardResponseAsync(int cardId)
    {
        var card = await db.Cards
            .Include(c => c.StateHistory)
            .FirstAsync(c => c.Id == cardId);
        return MapToResponse(card);
    }

    private static CardResponse MapToResponse(Card card) => new(
        card.Id,
        card.Title,
        card.Description,
        card.Position,
        card.ColumnId,
        card.StateHistory
            .OrderBy(h => h.EnteredAt)
            .Select(h => new CardStateHistoryResponse(
                h.ColumnId,
                h.ColumnName,
                h.EnteredAt,
                DateOnly.FromDateTime(h.EnteredAt),
                h.ExitedAt,
                h.ExitedAt.HasValue ? DateOnly.FromDateTime(h.ExitedAt.Value) : null))
    );

    private async Task<bool> IsBoardMemberAsync(int boardId, int userId) =>
        await db.BoardMembers.AnyAsync(m => m.BoardId == boardId && m.UserId == userId);

    private async Task<bool> BoardExistsAsync(int boardId) =>
        await db.Boards.AnyAsync(b => b.Id == boardId);
}
