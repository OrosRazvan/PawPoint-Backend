using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Interfaces
{
    public interface IFeedingService
    {
        Task<IReadOnlyList<FeedingResponse>> GetAllForUserAsync(int userId);
        Task<IReadOnlyList<FeedingResponse>> GetAllForAnimalAsync(int userId, int animalId);
        Task<FeedingResponse> GetByIdAsync(int userId, int feedingId);

        Task<FeedingResponse> CreateAsync(int userId, CreateFeedingRequest request);
        Task<FeedingResponse> UpdateAsync(int userId, int feedingId, UpdateFeedingRequest request);

        Task DeleteAsync(int userId, int feedingId);
    }
}
