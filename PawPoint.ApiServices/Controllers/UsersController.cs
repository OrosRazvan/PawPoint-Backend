using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PawPoint.ApiServices.Controllers
{
    [Authorize, Route("users")]
    public class UsersController(
        IIdentityService identityService,
        IUserService userService)
        : BaseApiController(identityService)
    {
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var profile = await userService.GetProfileAsync(GetUserIdFromToken());
            return Ok(profile);
        }

        [HttpPut("profile")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateUserProfileRequest request)
        {
            var updated = await userService.UpdateProfileAsync(GetUserIdFromToken(), request);
            return Ok(updated);
        }

        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            await userService.ChangePasswordAsync(GetUserIdFromToken(), request);
            return Ok(new { message = "Password changed." });
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await userService.GetSettingsAsync(GetUserIdFromToken());
            return Ok(settings);
        }

        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingsRequest request)
        {
            var updated = await userService.UpdateSettingsAsync(GetUserIdFromToken(), request);
            return Ok(updated);
        }

        [HttpDelete]
        public async Task<IActionResult> SoftDeleteUser()
        {
            await userService.SoftDeleteUserAsync(GetUserIdFromToken());
            return Ok(new { message = "User account has been deactivated." });
        }
    }
}
