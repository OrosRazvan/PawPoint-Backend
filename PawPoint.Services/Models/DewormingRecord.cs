namespace PawPoint.Services.Models;

public class DewormingRecord
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public string PetName { get; set; } = string.Empty;
    public string? DewormingType { get; set; }
    public DateTime DateGiven { get; set; }
    public DateTime? NextDueDate { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public int? IntervalDays { get; set; }
    public string? ProductName { get; set; }
    public string? VetName { get; set; }
    public string? Notes { get; set; }
}