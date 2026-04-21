using Microsoft.AspNetCore.Http;

namespace PawPoint.Services.Requests
{
    public sealed class CreateAnimalRequest
    {
        public required string Name { get; init; }
        public required string Species { get; init; }
        public string? Breed { get; init; }
        public double? WeightKg { get; init; }
        public DateTime? BirthDate { get; init; }
        public string? Sex { get; init; }
        public string? MicrochipNumber { get; init; }
        public IFormFile? Image { get; init; }
        public int? ImagePositionY { get; init; }
    }
}