using System.ComponentModel.DataAnnotations;

namespace PawPoint.Services.Requests
{
    public sealed class UpdateFeedingRequest
    {
        [MaxLength(200)]
        public string? Recipe { get; set; }

        [MaxLength(50)]
        public string? Quantity { get; set; }

        public DateTime? DateUtc { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
