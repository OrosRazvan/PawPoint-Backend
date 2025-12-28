using PawPoint.DB.Enums;

namespace PawPoint.Services.Requests
{
    public record NotificationCreateRequest(
        NotificationTypeEnum Type, 
        int UserId, 
        string Title, 
        string Content
        );
}