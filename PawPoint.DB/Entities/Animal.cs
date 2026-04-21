using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class Animal
    {
        [Key]
        public int Id { get; set; }

        // user-ul care deține animalul
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(50)]
        public required string Species { get; set; }  // Dog, Cat, etc.

        [MaxLength(100)]
        public string? Breed { get; set; }            // Border Collie, Maine Coon...

        public double? WeightKg { get; set; }

        public DateTime? BirthDate { get; set; }

        [MaxLength(10)]
        public string? Sex { get; set; }              // Male / Female etc.

        [MaxLength(100)]
        public string? MicrochipNumber { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }
        public int? ImagePositionY { get; set; }

        public bool IsDeleted { get; set; } = false;

        // legături spre celelalte tabele
        public ICollection<Vaccination> Vaccinations { get; set; } = [];
        public ICollection<Appointment> Appointments { get; set; } = [];
        public ICollection<Deworming> Dewormings { get; set; } = [];
        public ICollection<Feeding> Feedings { get; set; } = [];
    }
}
