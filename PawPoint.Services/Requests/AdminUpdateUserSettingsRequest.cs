namespace PawPoint.Services.Requests
{
    public sealed record AdminUpdateUserSettingsRequest(
        bool DarkMode,
        string TextSize,
        string WeightUnit,
        string DateFormat,
        int NotificationPreferenceId,
        bool EnableNotifications,
        bool VaccinationNotifications,
        bool AppointmentNotifications,
        bool DewormingNotifications,
        string NotificationBadgeMode
    );
}