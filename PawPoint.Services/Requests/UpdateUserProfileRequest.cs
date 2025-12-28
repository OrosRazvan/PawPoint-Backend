using Microsoft.AspNetCore.Http;

namespace PawPoint.Services.Requests
{
    public record UpdateUserProfileRequest(
        string? FullName,
        string? PhoneNumber,
        IFormFile? ProfilePicture
    );
}
