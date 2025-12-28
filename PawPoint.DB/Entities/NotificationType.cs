using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class NotificationType
    {
        [Key]
        public int Id { get; set; }
        [MaxLength(50)]
        public required string Name { get; set; }
        public ICollection<Notification> Notifications { get; set; } = [];
    }
}