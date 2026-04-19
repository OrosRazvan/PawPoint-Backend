using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class ContactMessage
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [MaxLength(200)]
        public string Email { get; set; } = null!;

        [MaxLength(200)]
        public string Title { get; set; } = null!;

        [MaxLength(4000)]
        public string Description { get; set; } = null!;

        [MaxLength(50)]
        public string Status { get; set; } = "Open";

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ContactMessageReply> Replies { get; set; } = [];
    }
}