using KanbanApi.Models;

namespace KanbanApi.Services;

public interface ICardService
{
    Task<ServiceResult<IEnumerable<CardResponse>>> GetCardsAsync(int boardId, int columnId, int userId, bool isAdmin = false);
    Task<ServiceResult<CardResponse>> CreateCardAsync(int boardId, int columnId, CreateCardRequest request, int userId, bool isAdmin = false);
    Task<ServiceResult<CardResponse>> UpdateCardAsync(int boardId, int columnId, int cardId, UpdateCardRequest request, int userId, bool isAdmin = false);
    Task<ServiceResult<bool>> DeleteCardAsync(int boardId, int columnId, int cardId, int userId, bool isAdmin = false);
    Task<ServiceResult<CardResponse>> MoveCardAsync(int boardId, int columnId, int cardId, MoveCardRequest request, int userId, bool isAdmin = false);
}
