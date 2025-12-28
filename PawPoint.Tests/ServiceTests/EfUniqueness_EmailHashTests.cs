using PawPoint.DB;
using PawPoint.DB.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PawPoint.Tests.ServiceTests
{
    public class EfUniqueness_EmailHashTests
    {
        private static Context NewDb()
        {
            var conn = new SqliteConnection("DataSource=:memory:");
            conn.Open();

            var options = new DbContextOptionsBuilder<Context>()
                .UseSqlite(conn)
                .Options;

            var ctx = new Context(options);
            ctx.Database.EnsureCreated(); 
            return ctx;
        }

        [Fact]
        public async System.Threading.Tasks.Task Unique_EmailHash_Is_Enforced()
        {
            using var db = NewDb();

            var pref = new NotificationPreference { Name = "All" };
            db.NotificationPreferences.Add(pref);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                FullName = "A",
                PasswordHash = "hash",
                Email = "cipher1",                  
                EmailHash = new string('a', 64),    
                NotificationPreferenceId = pref.Id,
                NotificationPreference = pref
            });
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                FullName = "B",
                PasswordHash = "hash",
                Email = "cipher2",
                EmailHash = new string('a', 64),
                NotificationPreferenceId = pref.Id,
                NotificationPreference = pref
            });

            await Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
        }
    }
}
