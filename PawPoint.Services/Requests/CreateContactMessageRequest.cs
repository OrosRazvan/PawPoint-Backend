namespace PawPoint.Services.Requests
{
    public sealed record CreateContactMessageRequest(
        string Title,
        string Description
    );
}