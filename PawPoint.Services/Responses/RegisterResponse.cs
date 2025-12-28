namespace PawPoint.Services.Responses
{
    public  record RegisterResponse(
        int UserId,
        string Email,
        string FullName,
        bool EmailConfirmed
    );
}

