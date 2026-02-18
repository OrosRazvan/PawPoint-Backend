using System.ComponentModel.DataAnnotations;
using PawPoint.DB.Enums;

namespace PawPoint.DB.Entities
{
    public class Deworming
    {
        [Key]
        public int Id { get; set; }

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        public DewormingTypeEnum Type { get; set; }

        public DateTime Date { get; set; }                 // data deparazitării

        public int IntervalDays { get; set; }              // la câte zile se repetă

        public DateTime? NextDate { get; set; }            // data următoare

        public int VetCabinetId { get; set; }
        public VetCabinet VetCabinet { get; set; } = null!;

        public int VetTimeSlotId { get; set; }
        public VetTimeSlot VetTimeSlot { get; set; } = null!;

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
