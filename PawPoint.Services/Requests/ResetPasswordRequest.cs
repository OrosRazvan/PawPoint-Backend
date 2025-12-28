namespace PawPoint.Services.Requests
{
    public record ResetPasswordRequest(string Token, string NewPassword);
}
