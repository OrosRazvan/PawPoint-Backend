namespace PawPoint.Services.Requests
{
    public sealed class UpdateVaccinationRequest
    {
        public string? VaccineName { get; set; }
        public string? Notes { get; set; }

        public DateTime? LastDate { get; set; }
        public DateTime? NextDate { get; set; }
    }
}
