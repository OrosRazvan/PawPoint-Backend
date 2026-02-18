using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Interfaces
{
    public interface IDewormingService
    {
        Task<IReadOnlyList<DewormingResponse>> GetAllForUserAsync(int userId);
        Task<IReadOnlyList<DewormingResponse>> GetAllForAnimalAsync(int userId, int animalId);
        Task<DewormingResponse> GetByIdAsync(int userId, int dewormingId);

        Task<DewormingResponse> CreateAsync(int userId, CreateDewormingRequest request);
        Task<DewormingResponse> UpdateAsync(int userId, int dewormingId, UpdateDewormingRequest request);

        Task DeleteAsync(int userId, int dewormingId);
    }
}
