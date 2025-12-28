using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class Deworming
    {
        [Key]
        public int Id { get; set; }

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        [MaxLength(100)]
        public required string Type { get; set; }          // External, Internal, Control, etc.

        public DateTime Date { get; set; }                 // data deparazitării

        public int IntervalDays { get; set; }              // la câte zile se repetă

        public DateTime? NextDate { get; set; }            // data următoare

        [MaxLength(200)]
        public string? ClinicName { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
