using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Interfaces
{
    public interface IContactMessageService
    {
        Task<ContactMessageItemResponse> CreateAsync(int userId, CreateContactMessageRequest request);
        Task<IReadOnlyList<ContactMessageItemResponse>> GetMyMessagesAsync(int userId);

        Task<IReadOnlyList<ContactMessageItemResponse>> GetAllAsync();
        Task<ContactMessageItemResponse> GetByIdAsync(int id);
        Task<ContactMessageReplyResponse> ReplyAsAdminAsync(int adminUserId, int messageId, ReplyContactMessageRequest request);
    }
}