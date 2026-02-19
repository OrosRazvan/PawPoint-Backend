namespace PawPoint.Services.Requests
{
    public sealed record UserSettingsUpdateRequest(
        bool? DarkMode,
        string? TextSize,
        string? WeightUnit,
        string? DateFormat
    );
}
