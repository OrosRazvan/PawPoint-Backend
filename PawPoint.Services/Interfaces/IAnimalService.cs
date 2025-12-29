using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Interfaces
{
    public interface IAnimalService
    {
        Task<AnimalResponse?> GetByIdAsync(int animalId, int userId);
        Task<IReadOnlyList<AnimalResponse>> GetMyAnimalsAsync(int userId);
        Task<AnimalResponse> CreateAsync(int userId, CreateAnimalRequest request);
        Task<AnimalResponse> UpdateAsync(int animalId, int userId, UpdateAnimalRequest request);
        Task DeleteAsync(int animalId, int userId);
    }
}
