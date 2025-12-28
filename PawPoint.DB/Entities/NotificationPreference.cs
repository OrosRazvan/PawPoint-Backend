using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class NotificationPreference
    {
        [Key]
        public int Id { get; set; }
        [MaxLength(50)]
        public required string Name { get; set; }
        public ICollection<User> Users { get; set; } = [];
    }
}