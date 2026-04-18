namespace PawPoint.Services.Requests
{
    public sealed record AdminSetUserPasswordRequest(
        string NewPassword
    );
}