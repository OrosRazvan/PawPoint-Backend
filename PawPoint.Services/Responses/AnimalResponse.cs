namespace PawPoint.Services.Responses
{
    public sealed record AnimalResponse(
        int Id,
        string Name,
        string Species,
        string? Breed,
        double? WeightKg,
        DateTime? BirthDate,
        string? Sex,
        string? MicrochipNumber,
        string? ImageUrl,
        int? ImagePositionY
    );
}