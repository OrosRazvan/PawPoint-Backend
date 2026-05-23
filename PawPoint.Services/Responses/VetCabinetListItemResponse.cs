using PawPoint.DB.Enums;

namespace PawPoint.Services.Responses
{
    public sealed record VetCabinetListItemResponse(
        int Id,
        string Name,
        string Address,
        string City,
        string PhoneNumber,
        string? Website,
        double Rating,
        double DistanceKm,
        decimal? Price,
        Currency Currency
    );
}