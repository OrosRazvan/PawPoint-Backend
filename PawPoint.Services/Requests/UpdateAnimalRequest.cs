using Microsoft.AspNetCore.Http;

namespace PawPoint.Services.Requests
{
    public sealed class UpdateAnimalRequest
    {
        public string? Name { get; init; }
        public string? Species { get; init; }
        public string? Breed { get; init; }
        public double? WeightKg { get; init; }
        public DateTime? BirthDate { get; init; }
        public string? Sex { get; init; }
        public string? MicrochipNumber { get; init; }
        public IFormFile? Image { get; init; }
        public bool RemoveImage { get; init; }
        public int? ImagePositionY { get; init; }
    }
}