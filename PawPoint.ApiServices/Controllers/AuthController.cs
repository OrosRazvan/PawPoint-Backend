using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using Microsoft.AspNetCore.Mvc;

namespace PawPoint.ApiServices.Controllers
{
    [Route("auth")]
    public class AuthController(IAuthService _auth, IIdentityService identityService) : BaseApiController(identityService)
    {
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
            => Ok(await _auth.RegisterAsync(request));

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
            => Redirect((await _auth.VerifyEmailAsync(request.Token)).RedirectUrl);

        [HttpGet("verify-email-link")]
        public async Task<IActionResult> VerifyEmailLink(string token)
            => Redirect((await _auth.VerifyEmailAsync(token)).RedirectUrl);

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
            => Ok(await _auth.LoginAsync(request));

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshRequest request)
            => Ok(await _auth.RefreshAsync(request));

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
            => Ok(await _auth.ForgotPasswordAsync(request));

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
            => Ok(await _auth.ResetPasswordAsync(request));

        [HttpDelete("expired-verification-tokens")]
        public async Task<IActionResult> DeleteExpiredVerificationTokens([FromQuery] bool systemRun = false)
            => Ok(await _auth.DeleteExpiredVerificationTokensAsync(systemRun));

    }
}
