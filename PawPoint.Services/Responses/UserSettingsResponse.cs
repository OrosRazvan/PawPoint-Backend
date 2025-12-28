namespace PawPoint.Services.Responses
{
    public record UserSettingsResponse(
        int NotificationPreferenceId,
        string NotificationPreference
    );
}
