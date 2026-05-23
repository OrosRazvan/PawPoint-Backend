using PawPoint.DB.Enums;

namespace PawPoint.Services.Requests
{
    public sealed class UpdateAppointmentRequest
    {
        public int? VetTimeSlotId { get; set; }

        public decimal? Price { get; set; }
        public Currency? Currency { get; set; }

        public string? Notes { get; set; }
        public string? Status { get; set; }
        public bool? Notify24hInAdvance { get; set; }
    }
}