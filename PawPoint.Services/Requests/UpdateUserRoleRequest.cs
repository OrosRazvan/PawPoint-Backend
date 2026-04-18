using PawPoint.DB.Enums;

namespace PawPoint.Services.Requests
{
    public sealed record UpdateUserRoleRequest(
        UserRoleEnum Role
    );
}