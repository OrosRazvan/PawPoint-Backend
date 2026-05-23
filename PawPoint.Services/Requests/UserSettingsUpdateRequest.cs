namespace PawPoint.Services.Requests
{
    public sealed record UserSettingsUpdateRequest(
        bool? DarkMode,
        string? TextSize,
        string? WeightUnit,
        string? DateFormat,
        string? Currency,
        bool? EnableNotifications,
        bool? VaccinationNotifications,
        bool? AppointmentNotifications,
        bool? DewormingNotifications,
        string? NotificationBadgeMode
    );
}