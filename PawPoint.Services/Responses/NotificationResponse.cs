namespace PawPoint.Services.Responses
{
    public record NotificationResponse(
        int Id,
        string Name,
        string Content,
        bool IsRead,
        DateTime CreatedAt,
        int TypeId,
        string TypeName
    );
}
