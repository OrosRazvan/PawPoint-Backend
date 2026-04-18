namespace PawPoint.Services.Responses
{
    public sealed record AdminUserItemResponse(
        int Id,
        string FullName,
        string Email,
        string Role,
        bool IsEmailConfirmed,
        bool IsDeleted,
        DateTime CreatedAt
    );
}