using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;

namespace PawPoint.ApiServices.Controllers
{
    [Authorize]
    [Route("settings")]
    public class UserSettingsController : BaseApiController
    {
        private readonly IUserSettingsService _settings;

        public UserSettingsController(IUserSettingsService settings, IIdentityService identityService)
            : base(identityService)
        {
            _settings = settings;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = GetUserIdFromToken();
            var result = await _settings.GetAsync(userId);
            return Ok(result);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UserSettingsUpdateRequest request)
        {
            var userId = GetUserIdFromToken();
            var result = await _settings.UpdateAsync(userId, request);
            return Ok(result);
        }
    }
}
