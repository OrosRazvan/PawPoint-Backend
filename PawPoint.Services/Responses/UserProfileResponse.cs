namespace PawPoint.Services.Responses
{
    public record UserProfileResponse(
       string Email,
       string FullName,
       string? ProfilePictureUrl,
       string? PhoneNumber,
       string NotificationPreference
   );
}
