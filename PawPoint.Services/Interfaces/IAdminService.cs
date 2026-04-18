using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Interfaces
{
    public interface IAdminService
    {
        Task<AdminDashboardResponse> GetDashboardAsync();
        Task<IReadOnlyList<AdminUserItemResponse>> GetUsersAsync();

        Task<AdminUserDetailsResponse> GetUserDetailsAsync(int userId);
        Task UpdateUserProfileAsync(int userId, AdminUpdateUserProfileRequest request);
        Task SetUserPasswordAsync(int userId, AdminSetUserPasswordRequest request);
        Task UpdateUserSettingsAsync(int userId, AdminUpdateUserSettingsRequest request);

        Task RestoreUserAsync(int userId);
        Task SoftDeleteUserAsync(int userId);
    }
}