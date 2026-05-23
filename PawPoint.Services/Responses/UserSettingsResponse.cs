namespace PawPoint.Services.Responses
{
    public sealed record UserSettingsResponse(
        int UserId,
        bool DarkMode,
        string Currency,
        string TextSize,
        string WeightUnit,
        string DateFormat,
        int NotificationPreferenceId,
        string NotificationPreference,
        bool EnableNotifications,
        bool VaccinationNotifications,
        bool AppointmentNotifications,
        bool DewormingNotifications,
        string NotificationBadgeMode
    );
}