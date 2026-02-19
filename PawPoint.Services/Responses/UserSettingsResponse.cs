namespace PawPoint.Services.Responses
{
    public sealed record UserSettingsResponse(
        int UserId,
        bool DarkMode,
        string TextSize,
        string WeightUnit,
        string DateFormat,
        int NotificationPreferenceId,
        string NotificationPreference
    );
}
