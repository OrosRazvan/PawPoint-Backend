using PawPoint.Services.Models;

namespace PawPoint.Services.Interfaces;

public interface IAssistantDataService
{
    Task<List<PetRecord>> GetPetsAsync(int userId, CancellationToken cancellationToken = default);
    Task<PetRecord?> GetPetByNameAsync(int userId, string petName, CancellationToken cancellationToken = default);

    Task<List<AppointmentRecord>> GetUpcomingAppointmentsAsync(
        int userId,
        int daysAhead = 30,
        CancellationToken cancellationToken = default);

    Task<List<AppointmentRecord>> GetAppointmentsForPetAsync(
        int userId,
        int petId,
        CancellationToken cancellationToken = default);

    Task<List<VaccinationRecord>> GetVaccinationsAsync(
        int userId,
        int? petId = null,
        CancellationToken cancellationToken = default);

    Task<List<DewormingRecord>> GetDewormingsAsync(
        int userId,
        int? petId = null,
        CancellationToken cancellationToken = default);
}