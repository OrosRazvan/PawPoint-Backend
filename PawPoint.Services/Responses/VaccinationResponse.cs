namespace PawPoint.Services.Responses
{
    public sealed record VaccinationResponse(
        int Id,
        int AnimalId,
        string AnimalName,
        string VaccineName,
        DateTime? LastDate,
        DateTime? NextDate,
        int VetCabinetId,
        string VetCabinetName,
        int VetTimeSlotId,
        DateTime SlotStartUtc,
        DateTime SlotEndUtc,
        string? Notes
    );
}
