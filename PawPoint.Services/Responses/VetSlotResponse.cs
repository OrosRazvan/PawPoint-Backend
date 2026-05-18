namespace PawPoint.Services.Responses
{
    public sealed record VetSlotResponse(
        int Id,
        DateTime StartTimeUtc,
        DateTime EndTimeUtc,
        int Capacity,
        int BookedCount,
        int AvailableCount
    );
}
