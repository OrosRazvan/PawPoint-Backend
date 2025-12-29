namespace PawPoint.Services.Requests
{
    public sealed class UpdateAppointmentRequest
    {
        // optional – doar dacă userul alege alt slot
        public int? VetTimeSlotId { get; set; }

        public decimal? EstimatedPriceRon { get; set; }
        public string? Notes { get; set; }
        public string? Status { get; set; }    // ex: "Confirmed", "Cancelled", "Rescheduled"
        public bool? Notify24hInAdvance { get; set; }
    }
}
