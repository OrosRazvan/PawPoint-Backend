using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class VetCabinet
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(200)]
        public required string Name { get; set; }

        [MaxLength(300)]
        public required string Address { get; set; }

        [MaxLength(100)]
        public required string City { get; set; }

        [MaxLength(50)]
        public string? PhoneNumber { get; set; }

        [MaxLength(300)]
        public string? Website { get; set; }

        public double Rating { get; set; } = 0;        // ex: 4.6
        public double DistanceKm { get; set; } = 0;    // ex: 3.1
        public decimal BasePriceRon { get; set; } = 0; // ex: 110 lei

        public ICollection<VetTimeSlot> TimeSlots { get; set; } = [];
        public ICollection<Appointment> Appointments { get; set; } = [];
        public ICollection<Vaccination> Vaccinations { get; set; } = [];

    }
}
