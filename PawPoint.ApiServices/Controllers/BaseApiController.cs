using PawPoint.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace PawPoint.ApiServices.Controllers
{
    [ApiController]
    public class BaseApiController(IIdentityService identityService) : ControllerBase
    {
        private readonly IIdentityService _identityService = identityService;
        protected int GetUserIdFromToken()
        {
            string? userIdString = _identityService.GetCurrentIdentity();
            if (string.IsNullOrEmpty(userIdString))
            {
                throw new InvalidOperationException("User identity could not be determined from the token.");
            }
            if (int.TryParse(userIdString, out int userId))
            {
                return userId;
            }
            else
            {
                throw new ArgumentException($"The user ID claim '{userIdString}' is not in a valid integer format.");
            }
        }
    }
}