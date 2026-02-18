namespace PawPoint.Services.Responses
{
    public sealed record DewormingResponse(
    int Id,
    int AnimalId,
    string AnimalName,
    string Type,
    DateTime DateUtc,
    int IntervalDays,
    DateTime? NextDateUtc,
    int VetCabinetId,
    string VetCabinetName,
    int VetTimeSlotId,
    DateTime SlotStartUtc,
    DateTime SlotEndUtc,
    string? Notes
    );
}
