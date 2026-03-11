using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KanbanApi.Services;

public class CardService(AppDbContext db, ILogger<CardService> logger) : ICardService
{
    public async Task<ServiceResult<IEnumerable<CardResponse>>> GetCardsAsync(int boardId, int columnId, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        if (await CheckBoardAccessAsync(boardId, userId, isAdmin, ct) is { } denied)
            return new ServiceResult<IEnumerable<CardResponse>>(default, denied);

        var column = await db.Columns
            .Include(c => c.Cards).ThenInclude(c => c.StateHistory)
            .FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId, ct);

        if (column is null) return ServiceResult<IEnumerable<CardResponse>>.NotFound();

        var cards = column.Cards
            .OrderBy(c => c.Position)
            .Select(MapToResponse);

        return ServiceResult<IEnumerable<CardResponse>>.Ok(cards);
    }

    public async Task<ServiceResult<CardResponse>> CreateCardAsync(int boardId, int columnId, CreateCardRequest request, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        if (await CheckBoardAccessAsync(boardId, userId, isAdmin, ct) is { } denied)
            return new ServiceResult<CardResponse>(default, denied);

        var column = await db.Columns
            .Include(c => c.Cards)
            .FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId, ct);

        if (column is null) return ServiceResult<CardResponse>.NotFound();

        if (column.WipLimit.HasValue && column.Cards.Count >= column.WipLimit.Value)
            return ServiceResult<CardResponse>.Conflict();

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
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created card {CardTitle} in column {ColumnId}", card.Title, columnId);
        return ServiceResult<CardResponse>.Ok(MapToResponse(card));
    }

    public async Task<ServiceResult<CardResponse>> UpdateCardAsync(int boardId, int columnId, int cardId, UpdateCardRequest request, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        if (await CheckBoardAccessAsync(boardId, userId, isAdmin, ct) is { } denied)
            return new ServiceResult<CardResponse>(default, denied);

        var card = await db.Cards
            .Include(c => c.StateHistory)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ColumnId == columnId && c.Column.BoardId == boardId, ct);

        if (card is null) return ServiceResult<CardResponse>.NotFound();

        if (request.Title is not null) card.Title = request.Title;
        if (request.Description is not null) card.Description = request.Description;
        if (request.Position.HasValue) card.Position = request.Position.Value;

        await db.SaveChangesAsync(ct);
        return ServiceResult<CardResponse>.Ok(MapToResponse(card));
    }

    public async Task<ServiceResult> DeleteCardAsync(int boardId, int columnId, int cardId, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        if (await CheckBoardAccessAsync(boardId, userId, isAdmin, ct) is { } denied)
            return new ServiceResult(denied);

        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ColumnId == columnId && c.Column.BoardId == boardId, ct);

        if (card is null) return ServiceResult.NotFound();

        db.Cards.Remove(card);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deleted card {CardId}", cardId);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<CardResponse>> MoveCardAsync(int boardId, int columnId, int cardId, MoveCardRequest request, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        if (await CheckBoardAccessAsync(boardId, userId, isAdmin, ct) is { } denied)
            return new ServiceResult<CardResponse>(default, denied);

        var card = await db.Cards
            .Include(c => c.StateHistory)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ColumnId == columnId && c.Column.BoardId == boardId, ct);

        if (card is null) return ServiceResult<CardResponse>.NotFound();

        var targetColumn = await db.Columns
            .Include(c => c.Cards)
            .FirstOrDefaultAsync(c => c.Id == request.TargetColumnId && c.BoardId == boardId, ct);
        if (targetColumn is null) return ServiceResult<CardResponse>.NotFound();

        if (columnId != request.TargetColumnId && targetColumn.WipLimit.HasValue && targetColumn.Cards.Count >= targetColumn.WipLimit.Value)
            return ServiceResult<CardResponse>.Conflict();

        var now = DateTime.UtcNow;

        var openState = card.StateHistory.FirstOrDefault(h => h.ExitedAt is null);
        if (openState is not null) openState.ExitedAt = now;

        card.StateHistory.Add(new CardStateHistory
        {
            CardId = card.Id,
            ColumnId = request.TargetColumnId,
            ColumnName = targetColumn.Name,
            EnteredAt = now
        });

        // Renormalize source column positions (if cross-column move)
        if (columnId != request.TargetColumnId)
        {
            var sourceCards = await db.Cards
                .Where(c => c.ColumnId == columnId && c.Id != cardId)
                .OrderBy(c => c.Position)
                .ToListAsync(ct);
            for (int i = 0; i < sourceCards.Count; i++)
                sourceCards[i].Position = i;
        }

        // Insert card at requested position and renormalize destination column
        card.ColumnId = request.TargetColumnId;
        var destCards = await db.Cards
            .Where(c => c.ColumnId == request.TargetColumnId && c.Id != cardId)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);
        destCards.Insert(Math.Clamp(request.Position, 0, destCards.Count), card);
        for (int i = 0; i < destCards.Count; i++)
            destCards[i].Position = i;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Moved card {CardId} to column {TargetColumnId}", cardId, request.TargetColumnId);
        return ServiceResult<CardResponse>.Ok(MapToResponse(card));
    }

    private async Task<ServiceStatus?> CheckBoardAccessAsync(int boardId, int userId, bool isAdmin, CancellationToken ct)
    {
        if (isAdmin) return null;
        if (!await db.BoardMembers.AnyAsync(m => m.BoardId == boardId && m.UserId == userId, ct))
            return await db.Boards.AnyAsync(b => b.Id == boardId, ct) ? ServiceStatus.Forbidden : ServiceStatus.NotFound;
        return null;
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
}
