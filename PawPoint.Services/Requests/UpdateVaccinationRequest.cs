using PawPoint.DB.Enums;

namespace PawPoint.Services.Requests
{
    public sealed class UpdateVaccinationRequest
    {
        public int? AnimalId { get; set; }

        public VaccineType? VaccineType { get; set; }

        public int? VetCabinetId { get; set; }

        public int? VetTimeSlotId { get; set; }

        public DateTime? LastDate { get; set; }

        public DateTime? NextDate { get; set; }

        public string? Notes { get; set; }
    }
}