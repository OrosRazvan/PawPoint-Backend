namespace PawPoint.Services.Responses
{
    public sealed record AdminAnimalDetailsResponse(
        Dictionary<string, object?> Animal,
        IReadOnlyList<Dictionary<string, object?>> Vaccinations,
        IReadOnlyList<Dictionary<string, object?>> Dewormings,
        IReadOnlyList<Dictionary<string, object?>> Feedings,
        IReadOnlyList<Dictionary<string, object?>> Appointments
    );
}