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
    }
}
