using PawPoint.DB.Enums;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Interfaces
{
    public interface IAuthService
    {
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        Task<VerifyEmailResponse> VerifyEmailAsync(string token);
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<TokenResponse> RefreshAsync(RefreshRequest request);
        Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
        Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request);
        Task<int> DeleteExpiredVerificationTokensAsync(bool systemRun, VerificationTokenEnum? type = null);
    }
}
