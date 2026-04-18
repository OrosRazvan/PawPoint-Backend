namespace PawPoint.Services.Responses
{
    public sealed record AdminUserDetailsResponse(
        Dictionary<string, object?> User,
        Dictionary<string, object?>? Settings,
        IReadOnlyList<AdminAnimalDetailsResponse> Animals
    );
}