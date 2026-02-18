using System.ComponentModel.DataAnnotations;

namespace PawPoint.Services.Requests
{
    public sealed class CreateFeedingRequest
    {
        public int AnimalId { get; set; }

        [MaxLength(200)]
        public required string Recipe { get; set; }

        [MaxLength(50)]
        public required string Quantity { get; set; }

        public DateTime DateUtc { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
