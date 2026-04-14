using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PawPoint.DB.Entities
{
    public class UserSettings
    {
        [Key]
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        public bool DarkMode { get; set; } = false;

        [MaxLength(20)]
        public string TextSize { get; set; } = "Medium"; 

        [MaxLength(10)]
        public string WeightUnit { get; set; } = "kg";   

        [MaxLength(20)]
        public string DateFormat { get; set; } = "DD/MM/YYYY";

        public bool EnableNotifications { get; set; } = true;
        public bool VaccinationNotifications { get; set; } = true;
        public bool AppointmentNotifications { get; set; } = true;
        public bool DewormingNotifications { get; set; } = true;
        public string NotificationBadgeMode { get; set; } = "count";

        public User User { get; set; } = default!;
    }
}
