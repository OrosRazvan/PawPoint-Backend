using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Interfaces
{
    public interface IUserService
    {
        Task<UserProfileResponse> GetProfileAsync(int userId);
        Task<UserProfileResponse> UpdateProfileAsync(int userId, UpdateUserProfileRequest request);
        Task ChangePasswordAsync(int userId, ChangePasswordRequest request);
        Task<UserSettingsResponse> GetSettingsAsync(int userId);
        Task<UserSettingsResponse> UpdateSettingsAsync(int userId, UpdateUserSettingsRequest request);
        Task SoftDeleteUserAsync(int userId);
    }
}
