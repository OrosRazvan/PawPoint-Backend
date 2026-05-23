using PawPoint.DB.Enums;

namespace PawPoint.Services.Requests
{
    public sealed class CreateAppointmentRequest
    {
        public int AnimalId { get; set; }
        public int VetCabinetId { get; set; }
        public int VetTimeSlotId { get; set; }

        public required string ServiceType { get; set; }

        public decimal? Price { get; set; }
        public Currency Currency { get; set; } = Currency.Eur;

        public string? Notes { get; set; }
        public bool Notify24hInAdvance { get; set; }
    }
}