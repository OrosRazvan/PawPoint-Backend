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

        public DateTime Date { get; set; }                

        public DateTime? NextDate { get; set; }           

        public int VetCabinetId { get; set; }
        public VetCabinet VetCabinet { get; set; } = null!;

        public int VetTimeSlotId { get; set; }
        public VetTimeSlot VetTimeSlot { get; set; } = null!;

        public decimal? Price { get; set; }
        public Currency Currency { get; set; } = Currency.Eur;

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
