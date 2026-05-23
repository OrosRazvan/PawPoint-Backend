using PawPoint.DB.Enums;
using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class Vaccination
    {
        [Key]
        public int Id { get; set; }

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        public VaccineType VaccineType { get; set; }

        public int VetCabinetId { get; set; }
        public VetCabinet VetCabinet { get; set; } = null!;

        public int VetTimeSlotId { get; set; }
        public VetTimeSlot VetTimeSlot { get; set; } = null!;

        public DateTime? LastDate { get; set; }          
        public DateTime? NextDate { get; set; }            

        [MaxLength(200)]
        public string? VeterinarianName { get; set; }

        [MaxLength(200)]
        public string? ClinicName { get; set; }

        public decimal? Price { get; set; }
        public Currency Currency { get; set; } = Currency.Eur;

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
