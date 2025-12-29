using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Interfaces
{
    public interface IAppointmentService
    {
        Task<IReadOnlyList<VetCabinetListItemResponse>> GetVetCabinetsAsync(
            string? serviceType,
            string? sortBy);

        Task<VetAvailabilityResponse> GetAvailabilityAsync(
            int vetCabinetId,
            DateOnly fromDate,
            DateOnly toDate);

        Task<IReadOnlyList<AppointmentResponse>> GetAllForUserAsync(int userId);
        Task<AppointmentResponse> GetByIdAsync(int userId, int appointmentId);

        Task<AppointmentResponse> CreateAsync(
            int userId,
            CreateAppointmentRequest request);

        Task<IReadOnlyList<AppointmentResponse>> GetAnimalPastAppointmentsAsync(
            int animalId,
            int userId);

        Task<IReadOnlyList<AppointmentResponse>> GetAnimalUpcomingAppointmentsAsync(
            int animalId,
            int userId);

        Task<AppointmentResponse> UpdateAsync(
            int appointmentId,
            int userId,
            UpdateAppointmentRequest request);
    }
}
