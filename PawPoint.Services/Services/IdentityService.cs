using PawPoint.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;

namespace PawPoint.Services.Services
{
    public class IdentityService(IHttpContextAccessor httpContextAccessor, ITokenService tokenService): IIdentityService
    {
        private string? _currentUser;

        public string? GetCurrentIdentity()
        {
            if (_currentUser == null)
            {
                string? authToken = GetToken();
                if (string.IsNullOrEmpty(authToken)) 
                {
                    return null;
                }
                _currentUser = GetUserIdByToken(authToken);
            }
            return _currentUser;
        }

        private string? GetToken()
        {
            var token = httpContextAccessor?.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
            return token?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                ? token["Bearer ".Length..].Trim()
                : token;
        }

        private string? GetUserIdByToken(string token)
        {
            ClaimsPrincipal? principal = tokenService.ValidateAccessToken(token);
            return principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}