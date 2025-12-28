using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }
        [MaxLength(50)]
        public required string Name { get; set; }
        [MaxLength(300)]
        public required string Content { get; set; }
        public bool IsRead { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int NotificationTypeId { get; set; }
        public NotificationType NotificationType { get; set; } = null!;
    }
}