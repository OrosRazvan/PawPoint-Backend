namespace PawPoint.Services.Models;

public class AppointmentRecord
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public string PetName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public string? VetName { get; set; }
    public string? VetCabinetName { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}