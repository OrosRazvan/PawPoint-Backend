using PawPoint.Services.Responses;
using System.Security.Claims;

namespace PawPoint.Services.Interfaces
{
    public interface ITokenService
    {
        TokenResponse IssueTokens(int userId, string email, string role);
        ClaimsPrincipal? ValidateAccessToken(string token);
        ClaimsPrincipal? ValidateRefreshToken(string token);
    }
}
