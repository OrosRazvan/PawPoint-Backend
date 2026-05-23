using PawPoint.DB.Enums;

namespace PawPoint.Services.Responses
{
    public sealed record DewormingResponse(
        int Id,
        int AnimalId,
        string AnimalName,
        DewormingTypeEnum Type,
        DateTime DateUtc,
        DateTime? NextDateUtc,
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