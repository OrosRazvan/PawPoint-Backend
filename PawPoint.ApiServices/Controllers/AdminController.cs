using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;

namespace PawPoint.ApiServices.Controllers
{
    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("admin")]
    public class AdminController(
        IAdminService adminService,
        IIdentityService identityService)
        : BaseApiController(identityService)
    {
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var response = await adminService.GetDashboardAsync();
            return Ok(response);
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var response = await adminService.GetUsersAsync();
            return Ok(response);
        }

        [HttpGet("users/{id:int}")]
        public async Task<IActionResult> GetUserDetails(int id)
        {
            var response = await adminService.GetUserDetailsAsync(id);
            return Ok(response);
        }

        [HttpPut("users/{id:int}/profile")]
        public async Task<IActionResult> UpdateUserProfile(int id, [FromBody] AdminUpdateUserProfileRequest request)
        {
            await adminService.UpdateUserProfileAsync(id, request);
            return Ok(new { message = "User profile updated." });
        }

        [HttpPut("users/{id:int}/password")]
        public async Task<IActionResult> SetUserPassword(int id, [FromBody] AdminSetUserPasswordRequest request)
        {
            await adminService.SetUserPasswordAsync(id, request);
            return Ok(new { message = "User password updated." });
        }

        [HttpPut("users/{id:int}/settings")]
        public async Task<IActionResult> UpdateUserSettings(int id, [FromBody] AdminUpdateUserSettingsRequest request)
        {
            await adminService.UpdateUserSettingsAsync(id, request);
            return Ok(new { message = "User settings updated." });
        }

        [HttpPut("users/{id:int}/restore")]
        public async Task<IActionResult> RestoreUser(int id)
        {
            await adminService.RestoreUserAsync(id);
            return Ok(new { message = "User restored." });
        }

        [HttpDelete("users/{id:int}")]
        public async Task<IActionResult> SoftDeleteUser(int id)
        {
            await adminService.SoftDeleteUserAsync(id);
            return Ok(new { message = "User deactivated." });
        }
    }
}