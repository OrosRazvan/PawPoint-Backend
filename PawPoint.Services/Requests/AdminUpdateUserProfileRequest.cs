namespace PawPoint.Services.Requests
{
    public sealed record AdminUpdateUserProfileRequest(
        string FullName,
        string Email,
        string? PhoneNumber,
        bool IsEmailConfirmed
    );
}