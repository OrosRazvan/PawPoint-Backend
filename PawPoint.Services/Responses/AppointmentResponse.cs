namespace PawPoint.Services.Responses
{
    public sealed record AppointmentResponse(
        int Id,
        int AnimalId,
        string AnimalName,
        int VetCabinetId,
        string VetCabinetName,
        int VetTimeSlotId,
        DateTime StartTimeUtc,
        DateTime EndTimeUtc,
        string ServiceType,
        decimal? EstimatedPriceRon,
        string Status,
        bool Notify24hInAdvance
    );
}
