namespace PawPoint.Services.Requests
{
    public sealed class CreateAppointmentRequest
    {
        public int AnimalId { get; set; }
        public int VetCabinetId { get; set; }
        public int VetTimeSlotId { get; set; }

        // "Consult", "Vaccination", "Deworming" etc.
        public required string ServiceType { get; set; }

        public decimal? EstimatedPriceRon { get; set; }
        public string? Notes { get; set; }
        public bool Notify24hInAdvance { get; set; }
    }
}
