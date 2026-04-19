using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class ContactMessageReply
    {
        [Key]
        public int Id { get; set; }

        public int ContactMessageId { get; set; }
        public ContactMessage ContactMessage { get; set; } = null!;

        public int SenderUserId { get; set; }
        public User SenderUser { get; set; } = null!;

        [MaxLength(50)]
        public string SenderType { get; set; } = null!; // "Admin" sau "User"

        [MaxLength(4000)]
        public string Message { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}