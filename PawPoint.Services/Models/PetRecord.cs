namespace PawPoint.Services.Models;

public class PetRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string? Breed { get; set; }
    public DateTime? BirthDate { get; set; }
    public double? WeightKg { get; set; }
    public string? Sex { get; set; }
}