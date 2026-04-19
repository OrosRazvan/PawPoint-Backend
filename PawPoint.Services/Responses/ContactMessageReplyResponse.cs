namespace PawPoint.Services.Responses
{
    public sealed record ContactMessageReplyResponse(
        int Id,
        int SenderUserId,
        string SenderType,
        string SenderName,
        string Message,
        DateTime CreatedAt
    );
}