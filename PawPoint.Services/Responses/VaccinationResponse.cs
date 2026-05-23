using PawPoint.DB.Enums;

namespace PawPoint.Services.Responses
{
    public sealed record VaccinationResponse(
        int Id,
        int AnimalId,
        string AnimalName,
        VaccineType VaccineType,
        DateTime? LastDate,
        DateTime? NextDate,
        int VetCabinetId,
        string VetCabinetName,
        int VetTimeSlotId,
        DateTime SlotStartUtc,
        DateTime SlotEndUtc,
        decimal? Price,
        Currency Currency,
        string? Notes
    );
}