using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class Vaccination
    {
        [Key]
        public int Id { get; set; }

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        [MaxLength(100)]
        public required string VaccineName { get; set; }   

        public DateTime? LastDate { get; set; }          
        public DateTime? NextDate { get; set; }            

        [MaxLength(200)]
        public string? VeterinarianName { get; set; }

        [MaxLength(200)]
        public string? ClinicName { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
