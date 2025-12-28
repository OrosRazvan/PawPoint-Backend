namespace PawPoint.Services.Responses
{
    public record LoginResponse(
        int UserId,
        string Email,
        string FullName,
        TokenResponse Tokens
    );
}
