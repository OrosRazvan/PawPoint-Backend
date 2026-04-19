namespace PawPoint.Services.Responses
{
    public sealed record ContactMessageItemResponse(
        int Id,
        int UserId,
        string UserFullName,
        string Email,
        string Title,
        string Description,
        string Status,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        IReadOnlyList<ContactMessageReplyResponse> Replies
    );
}