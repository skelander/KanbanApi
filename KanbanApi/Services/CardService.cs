using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services;

public class CardService(AppDbContext db) : ICardService
{
    public async Task<ServiceResult<IEnumerable<CardResponse>>> GetCardsAsync(int boardId, int columnId, int userId)
    {
        if (!await IsBoardMemberAsync(boardId, userId))
            return await BoardExistsAsync(boardId)
                ? ServiceResult<IEnumerable<CardResponse>>.Forbidden()
                : ServiceResult<IEnumerable<CardResponse>>.NotFound();

        var column = await db.Columns
            .Include(c => c.Cards).ThenInclude(c => c.StateHistory).ThenInclude(h => h.Column)
            .FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId);

        if (column is null) return ServiceResult<IEnumerable<CardResponse>>.NotFound();

        var cards = column.Cards
            .OrderBy(c => c.Position)
            .Select(MapToResponse);

        return ServiceResult<IEnumerable<CardResponse>>.Ok(cards);
    }

    public async Task<ServiceResult<CardResponse>> CreateCardAsync(int boardId, int columnId, CreateCardRequest request, int userId)
    {
        if (!await IsBoardMemberAsync(boardId, userId))
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

        db.Cards.Add(card);
        await db.SaveChangesAsync();

        db.CardStateHistories.Add(new CardStateHistory
        {
            CardId = card.Id,
            ColumnId = columnId,
            EnteredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return ServiceResult<CardResponse>.Ok(await LoadCardResponseAsync(card.Id));
    }

    public async Task<ServiceResult<CardResponse>> UpdateCardAsync(int boardId, int columnId, int cardId, UpdateCardRequest request, int userId)
    {
        if (!await IsBoardMemberAsync(boardId, userId))
            return await BoardExistsAsync(boardId)
                ? ServiceResult<CardResponse>.Forbidden()
                : ServiceResult<CardResponse>.NotFound();

        var card = await db.Cards
            .Include(c => c.StateHistory).ThenInclude(h => h.Column)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ColumnId == columnId && c.Column.BoardId == boardId);

        if (card is null) return ServiceResult<CardResponse>.NotFound();

        if (request.Title is not null) card.Title = request.Title;
        if (request.Description is not null) card.Description = request.Description;
        if (request.Position.HasValue) card.Position = request.Position.Value;

        await db.SaveChangesAsync();
        return ServiceResult<CardResponse>.Ok(MapToResponse(card));
    }

    public async Task<ServiceResult<bool>> DeleteCardAsync(int boardId, int columnId, int cardId, int userId)
    {
        if (!await IsBoardMemberAsync(boardId, userId))
            return await BoardExistsAsync(boardId)
                ? ServiceResult<bool>.Forbidden()
                : ServiceResult<bool>.NotFound();

        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ColumnId == columnId && c.Column.BoardId == boardId);

        if (card is null) return ServiceResult<bool>.NotFound();

        db.Cards.Remove(card);
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<CardResponse>> MoveCardAsync(int boardId, int columnId, int cardId, MoveCardRequest request, int userId)
    {
        if (!await IsBoardMemberAsync(boardId, userId))
            return await BoardExistsAsync(boardId)
                ? ServiceResult<CardResponse>.Forbidden()
                : ServiceResult<CardResponse>.NotFound();

        var card = await db.Cards
            .Include(c => c.StateHistory).ThenInclude(h => h.Column)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ColumnId == columnId && c.Column.BoardId == boardId);

        if (card is null) return ServiceResult<CardResponse>.NotFound();

        var targetColumn = await db.Columns.FirstOrDefaultAsync(c => c.Id == request.TargetColumnId && c.BoardId == boardId);
        if (targetColumn is null) return ServiceResult<CardResponse>.NotFound();

        var now = DateTime.UtcNow;

        // Close the current open state record
        var openState = card.StateHistory.FirstOrDefault(h => h.ExitedAt is null);
        if (openState is not null) openState.ExitedAt = now;

        // Open a new state record for the target column
        db.CardStateHistories.Add(new CardStateHistory
        {
            CardId = card.Id,
            ColumnId = request.TargetColumnId,
            EnteredAt = now
        });

        card.ColumnId = request.TargetColumnId;
        card.Position = request.Position;

        await db.SaveChangesAsync();
        return ServiceResult<CardResponse>.Ok(await LoadCardResponseAsync(card.Id));
    }

    private async Task<CardResponse> LoadCardResponseAsync(int cardId)
    {
        var card = await db.Cards
            .Include(c => c.StateHistory).ThenInclude(h => h.Column)
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
                h.Column.Name,
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
