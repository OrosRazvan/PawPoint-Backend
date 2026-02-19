using PawPoint.DB.Enums;
using System.ComponentModel.DataAnnotations;

namespace PawPoint.DB.Entities
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        [MaxLength(800)]
        public required string Email { get; set; }
        [MaxLength(64)]
        public required string EmailHash { get; set; }
        [MaxLength(200)]
        public required string PasswordHash { get; set; }
        [MaxLength(200)]
        public required string FullName { get; set; }
        [MaxLength(500)]
        public string? ProfilePictureUrl { get; set; }
        [MaxLength(800)]
        public string? PhoneNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public bool IsEmailConfirmed { get; set; } = false;
        public bool Has2FA { get; set; } = false;
        public int NotificationPreferenceId { get; set; }
        public NotificationPreference NotificationPreference { get; set; } = null!;
        public ICollection<Notification> Notifications { get; set; } = [];
        public ICollection<VerificationToken> VerificationTokens { get; set; } = [];
        public ICollection<Animal> Animals { get; set; } = [];
        public UserSettings? Settings { get; set; }

    }
}