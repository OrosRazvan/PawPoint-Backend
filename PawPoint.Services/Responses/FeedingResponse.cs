namespace PawPoint.Services.Responses
{
    public sealed record FeedingResponse(
        int Id,
        int AnimalId,
        string AnimalName,
        string Recipe,
        string Quantity,
        DateTime DateUtc,
        string? Notes
    );
}
