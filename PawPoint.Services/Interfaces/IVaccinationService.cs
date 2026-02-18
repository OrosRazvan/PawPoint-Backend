using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Interfaces
{
    public interface IVaccinationService
    {
        Task<IReadOnlyList<VaccinationResponse>> GetAllForUserAsync(int userId);
        Task<IReadOnlyList<VaccinationResponse>> GetAllForAnimalAsync(int userId, int animalId);
        Task<VaccinationResponse> GetByIdAsync(int userId, int vaccinationId);

        Task<VaccinationResponse> CreateAsync(int userId, CreateVaccinationRequest request);
        Task<VaccinationResponse> UpdateAsync(int userId, int vaccinationId, UpdateVaccinationRequest request);

        Task DeleteAsync(int userId, int vaccinationId);
    }
}
