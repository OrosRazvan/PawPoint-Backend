namespace PawPoint.Services.Requests
{
   public record RegisterRequest(
        string FullName,
        string Email,
        string Password
    );
}
