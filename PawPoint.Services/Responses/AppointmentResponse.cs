namespace PawPoint.Services.Responses
{
    public sealed record AppointmentResponse(
        int Id,
        int AnimalId,
        string AnimalName,
        int VetCabinetId,
        string VetCabinetName,
        string VetCabinetAddress,
        int VetTimeSlotId,
        DateTime SlotStartTimeUtc,
        DateTime SlotEndTimeUtc,
        string ServiceType,
        decimal? PriceRon,
        string Status,
        bool Notify24hInAdvance
    );
}