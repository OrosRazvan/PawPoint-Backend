using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class VetTimeSlot
    {
        [Key]
        public int Id { get; set; }

        public int VetCabinetId { get; set; }
        public VetCabinet VetCabinet { get; set; } = null!;

        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }

        public int Capacity { get; set; } = 1;

        public int BookedCount { get; set; } = 0;

        public ICollection<Appointment> Appointments { get; set; } = [];
    }
}
