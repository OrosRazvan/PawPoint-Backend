namespace PawPoint.Services.Models;

public class VaccinationRecord
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public string PetName { get; set; } = string.Empty;
    public string? VaccineName { get; set; }
    public DateTime DateGiven { get; set; }
    public DateTime? NextDueDate { get; set; }
    public string? VetName { get; set; }
    public string? Notes { get; set; }
}