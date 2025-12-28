namespace PawPoint.Services.Responses
{
    public record TokenResponse(
        string AccessToken,
        string RefreshToken,
        DateTime AccessExpiresAtUtc,
        DateTime RefreshExpiresAtUtc
    );
}
