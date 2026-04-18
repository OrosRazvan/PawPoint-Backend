namespace PawPoint.Services.Responses
{
    public sealed record AdminDashboardResponse(
        int TotalUsers,
        int ActiveUsers,
        int DeletedUsers,
        int TotalAnimals,
        int TotalAppointments,
        int TotalVetCabinets
    );
}