using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class VerificationToken
    {
        [Key]
        public int Id { get; set; }
        public required string Token { get; set; }
        public int VerificationTokenTypeId { get; set; }
        public VerificationTokenType VerificationTokenType { get; set; } = null!;
        public DateTime ExpirationDate { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public int UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
