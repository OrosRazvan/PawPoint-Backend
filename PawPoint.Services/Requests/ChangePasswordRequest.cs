namespace PawPoint.Services.Requests
{
    public record ChangePasswordRequest(
        string CurrentPassword,
        string NewPassword
    );
}
