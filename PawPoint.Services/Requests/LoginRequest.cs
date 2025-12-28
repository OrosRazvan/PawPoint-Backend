namespace PawPoint.Services.Requests
{
    public record LoginRequest(
        string Email,
        string Password
    );
}
