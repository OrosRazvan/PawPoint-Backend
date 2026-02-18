namespace PawPoint.Services.Requests
{
    public sealed class CreateVaccinationRequest
    {
        public int AnimalId { get; set; }
        public required string VaccineName { get; set; }

        public int VetCabinetId { get; set; }
        public int VetTimeSlotId { get; set; }

        public string? Notes { get; set; }

        public DateTime? LastDate { get; set; }
        public DateTime? NextDate { get; set; }
    }
}
