namespace PawPoint.Services.Responses
{
    public record VerifyEmailResponse(
        bool Success,
        string RedirectUrl,
        string? Message
    );
}
