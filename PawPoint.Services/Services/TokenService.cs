using PawPoint.Common.Helpers;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Responses;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PawPoint.Services.Services
{
    public class TokenService(IOptions<JwtSettings> jwtOptions) : ITokenService
    {
        private readonly JwtSettings _jwt = jwtOptions.Value;

        public TokenResponse IssueTokens(int userId, string email)
        {
            ValidateIssueTokensInput(userId, email);

            var now = DateTime.UtcNow;
            var accessExp = now.AddMinutes(_jwt.AccessExpiresInMinutes);
            var refreshExp = now.AddMinutes(_jwt.RefreshExpiresInMinutes);

            return new TokenResponse(
                AccessToken: CreateJwt(_jwt.AccessSecret, accessExp, userId, email, "access"),
                RefreshToken: CreateJwt(_jwt.RefreshSecret, refreshExp, userId, email, "refresh"),
                AccessExpiresAtUtc: accessExp,
                RefreshExpiresAtUtc: refreshExp
            );
        }

        public ClaimsPrincipal? ValidateAccessToken(string token)
            => ValidateTokenInternal(token, expectedTyp: "access", secret: _jwt.AccessSecret);

        public ClaimsPrincipal? ValidateRefreshToken(string token)
            => ValidateTokenInternal(token, expectedTyp: "refresh", secret: _jwt.RefreshSecret);

        private static void ValidateIssueTokensInput(int userId, string email)
        {
            var emailBlank = string.IsNullOrWhiteSpace(email);
            var emailValid = !emailBlank && System.Net.Mail.MailAddress.TryCreate(email, out _);

            _ = (userId, emailBlank, emailValid) switch
            {
                ( <= 0, _, _) => throw new ArgumentException("userId must be a positive integer.", nameof(userId)),
                (_, true, _) => throw new ArgumentException("Email is required.", nameof(email)),
                (_, _, false) => throw new ArgumentException("Email format is invalid.", nameof(email)),
                _ => true
            };
        }

        private ClaimsPrincipal? ValidateTokenInternal(string token, string expectedTyp, string secret)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),

                ValidateIssuer = true,
                ValidIssuer = _jwt.Issuer,

                ValidateAudience = true,
                ValidAudience = _jwt.Audience,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, parameters, out var securityToken);

                if (securityToken is not JwtSecurityToken jwt ||
                    !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var typ = principal.FindFirst("typ")?.Value;
                if (!string.Equals(typ, expectedTyp, StringComparison.Ordinal))
                {
                    return null;
                }

                return principal;
            }
            catch
            {
                return null;
            }
        }

        private string CreateJwt(string secret, DateTime expiresUtc, int userId, string email, string typ)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim("typ", typ),
                new Claim(ClaimTypes.Sid, Guid.NewGuid().ToString("N")), 
            };

            var creds = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256);

            var jwt = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresUtc,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }
    }
}
