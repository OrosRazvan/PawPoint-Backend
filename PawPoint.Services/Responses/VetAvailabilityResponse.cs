namespace PawPoint.Services.Responses
{
    public sealed record VetDayAvailabilityResponse(
        DateOnly Date,
        IReadOnlyList<VetSlotResponse> Slots
    );

    public sealed record VetAvailabilityResponse(
        int VetCabinetId,
        string VetCabinetName,
        IReadOnlyList<VetDayAvailabilityResponse> Days
    );
}
