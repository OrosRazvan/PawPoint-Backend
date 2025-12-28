using PawPoint.DB;
using PawPoint.DB.Entities;
using Microsoft.EntityFrameworkCore;
using TaskThreading = System.Threading.Tasks.Task;

namespace PawPoint.ApiServices.Helpers
{
    public class DatabaseSeedService : IDatabaseSeedService
    {
        public void MigrateDatabase(IServiceScope serviceScope)
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<Context>();

            // Apply pending migrations
            if (context.Database.GetPendingMigrations().Any())
            {
                var strat = context.Database.CreateExecutionStrategy();
                strat.Execute(() => context.Database.Migrate());            
            }

            // Always attempt to seed (flag ensures one-time run per DB)
            SeedDatabase(context).GetAwaiter().GetResult();
        }

        public async TaskThreading SeedDatabase(Context database)
        {
            var seedRecord = await database.SeedStatuses.FirstOrDefaultAsync();
            if (seedRecord == null)
            {
                seedRecord = new SeedStatus { ShouldSeedDatabase = false };
                database.SeedStatuses.Add(seedRecord);
                await database.SaveChangesAsync();
            }
            else if (!seedRecord.ShouldSeedDatabase)
            {
                return;
            }

            await SeedNotificationPreferences(database);
            await SeedNotificationTypes(database);
            await SeedVerificationTokenTypes(database);
            await SeedVetCabinets(database);

            await database.SaveChangesAsync();
        }

        private static async TaskThreading SeedNotificationPreferences(Context database)
        {
            var notificationPreferences = new List<NotificationPreference>
            {
                new NotificationPreference {Name = "All" },
                new NotificationPreference {Name = "InvitesOnly" },
                new NotificationPreference {Name = "Mute" }
            };
            await database.NotificationPreferences.AddRangeAsync(notificationPreferences);
        }

        private static async TaskThreading SeedVerificationTokenTypes(Context database)
        {
            var verificationTokenTypes = new List<VerificationTokenType>
            {
                new VerificationTokenType {Name = "ForgotPasswordToken" },
                new VerificationTokenType {Name = "EmailVerificationToken" }
            };
            await database.VerificationTokenTypes.AddRangeAsync(verificationTokenTypes);
        }

        private static async TaskThreading SeedNotificationTypes(Context database)
        {
            var notificationTypes = new List<NotificationType>
            {
                new NotificationType {Name = "Reminder" },
                new NotificationType {Name = "Alert" },
                new NotificationType {Name = "Invitation" },
                new NotificationType {Name = "Commercial" }
            };
            await database.NotificationTypes.AddRangeAsync(notificationTypes);
        }

        private static async TaskThreading SeedVetCabinets(Context database)
        {
            var cabinets = new List<VetCabinet>
            {
                new() { Name = "HappyPaws Clinic", Address = "Str. Mihai Eminescu 15", City = "Cluj-Napoca", PhoneNumber="0721 111 222", Website="happypaws.ro", Rating = 4.7, DistanceKm = 3.2, BasePriceRon = 120 },
                new() { Name = "VetPlus Center", Address = "Str. Memorandumului 9", City = "Cluj-Napoca", PhoneNumber="0743 883 221", Website="vetplus.ro", Rating = 4.8, DistanceKm = 2.5, BasePriceRon = 140 },
                new() { Name = "MiauWoof Care", Address = "Bd. Eroilor 48", City = "Cluj-Napoca", PhoneNumber="0752 667 991", Website="miauwow.ro", Rating = 4.9, DistanceKm = 1.7, BasePriceRon = 150 },
                new() { Name = "ABC Pet Clinic", Address = "Str. Universității 21", City = "Cluj-Napoca", PhoneNumber="0728 552 120", Website="abcpetclinic.ro", Rating = 4.3, DistanceKm = 4.3, BasePriceRon = 110 },
                new() { Name = "PetLife Medical", Address = "Str. București 100", City = "Cluj-Napoca", PhoneNumber="0758 303 919", Website="petlife.ro", Rating = 4.5, DistanceKm = 6.1, BasePriceRon = 135 },
                new() { Name = "RoyalVets", Address = "Str. Câmpului 9", City = "Cluj-Napoca", PhoneNumber="0733 200 890", Website="royalvets.ro", Rating = 4.8, DistanceKm = 5.0, BasePriceRon = 155 },
                new() { Name = "PetDoctor Clinic", Address = "Str. Primăverii 78", City = "Cluj-Napoca", PhoneNumber="0749 982 112", Website="petdoctor.ro", Rating = 4.4, DistanceKm = 3.9, BasePriceRon = 118 },
                new() { Name = "Animavet Medical Center", Address = "Str. Florilor 12", City = "Cluj-Napoca", PhoneNumber="0763 202 392", Website="animavet.ro", Rating = 4.5, DistanceKm = 2.3, BasePriceRon = 130 },
                new() { Name = "GreenPaws Veterinary", Address = "Str. Morii 2", City = "Cluj-Napoca", PhoneNumber="0738 903 113", Website="greenpaws.ro", Rating = 4.6, DistanceKm = 2.8, BasePriceRon = 145 },
                new() { Name = "Doggo Diagnostics", Address = "Str. Observatorului 31", City = "Cluj-Napoca", PhoneNumber="0799 199 111", Website="doggodiag.ro", Rating = 4.7, DistanceKm = 3.5, BasePriceRon = 160 },
                new() { Name = "Feline Focus", Address = "Str. Ciobanului 3", City = "Cluj-Napoca", PhoneNumber="0712 333 908", Website="felinefocus.ro", Rating = 4.9, DistanceKm = 4.9, BasePriceRon = 170 },
                new() { Name = "Cat & Dog Health", Address = "Str. Culturii 5", City = "Cluj-Napoca", PhoneNumber="0720 805 002", Website="catdoghealth.ro", Rating = 4.2, DistanceKm = 6.7, BasePriceRon = 115 },
                new() { Name = "PetWell Center", Address = "Str. Avram Iancu 234", City = "Cluj-Napoca", PhoneNumber="0737 445 908", Website="petwellcenter.ro", Rating = 4.6, DistanceKm = 4.4, BasePriceRon = 138 },
                new() { Name = "Animalia Vet", Address = "Str. Jupiter 47", City = "Cluj-Napoca", PhoneNumber="0762 551 999", Website="animalia.ro", Rating = 4.3, DistanceKm = 7.2, BasePriceRon = 125 },
                new() { Name = "Companion Care Clinic", Address = "Str. Someșului 13", City = "Cluj-Napoca", PhoneNumber="0744 230 113", Website="companioncare.ro", Rating = 4.8, DistanceKm = 3.8, BasePriceRon = 150 }
            };

            await database.VetCabinets.AddRangeAsync(cabinets);
        }
    }
}