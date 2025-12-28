using PawPoint.DB;

namespace PawPoint.ApiServices.Helpers
{
    public interface IDatabaseSeedService
    {
        void MigrateDatabase(IServiceScope serviceScope);
        Task SeedDatabase(Context database);
    }
}
