using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawPoint.DB;
using PawPoint.DB.Entities;
using TaskThreading = System.Threading.Tasks.Task;

namespace PawPoint.ApiServices.Helpers
{
    public class DatabaseSeedService : IDatabaseSeedService
    {
        public void MigrateDatabase(IServiceScope serviceScope)
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<Context>();

            var strat = context.Database.CreateExecutionStrategy();
            strat.Execute(() => context.Database.Migrate());

            SeedDatabase(context).GetAwaiter().GetResult();
        }

        public async TaskThreading SeedDatabase(Context database)
        {
            SeedStatus? seedRecord;

            try
            {
                seedRecord = await database.SeedStatuses.FirstOrDefaultAsync();
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                // SeedStatuses nu există -> migrațiile nu au creat schema (încă).
                // Nu mai crăpăm aplicația.
                return;
            }

            if (seedRecord == null)
            {
                seedRecord = new SeedStatus { ShouldSeedDatabase = true };
                database.SeedStatuses.Add(seedRecord);
                await database.SaveChangesAsync();
            }

            if (!seedRecord.ShouldSeedDatabase)
                return;

            await SeedNotificationPreferences(database);
            await SeedNotificationTypes(database);
            await SeedVerificationTokenTypes(database);
            await database.SaveChangesAsync();

            await SeedVetCabinets(database);
            await database.SaveChangesAsync();

            await SeedVetTimeSlots(database);
            await database.SaveChangesAsync();

            seedRecord.ShouldSeedDatabase = false;
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

            var existingNames = await database.VetCabinets
                .Select(c => c.Name)
                .ToListAsync();

            var toInsert = cabinets
                .Where(c => !existingNames.Contains(c.Name))
                .ToList();

            if (toInsert.Count > 0)
            {
                await database.VetCabinets.AddRangeAsync(toInsert);
            }
        }

        private static async TaskThreading SeedVetTimeSlots(Context database)
        {
            const int MaxTotalSlots = 200;

            // dacă avem deja 200 sau mai multe, nu mai facem nimic
            var existingCount = await database.VetTimeSlots.CountAsync();
            if (existingCount >= MaxTotalSlots)
                return;

            var cabinets = await database.VetCabinets.ToListAsync();
            if (cabinets.Count == 0) return;

            var rng = new Random();

            // nu mai mergem 3 luni, ca să nu fie super risipit
            var nowDate = DateTime.UtcNow.Date;
            var endDate = nowDate.AddMonths(1);

            var newSlots = new List<VetTimeSlot>();
            var usedKeys = new HashSet<string>();

            var totalSlots = existingCount; // de obicei 0 după ce ai șters

            for (var date = nowDate; date < endDate && totalSlots < MaxTotalSlots; date = date.AddDays(1))
            {
                foreach (var cabinet in cabinets)
                {
                    if (totalSlots >= MaxTotalSlots)
                        break;

                    // ~50% din cabinete vor avea program în ziua asta
                    if (rng.NextDouble() >= 0.5)
                        continue;

                    var slotsPerDay = rng.Next(1, 4); // 1–3 sloturi / cabinet / zi

                    for (int i = 0; i < slotsPerDay && totalSlots < MaxTotalSlots; i++)
                    {
                        var hour = rng.Next(8, 18);        // 8–17
                        var minute = rng.Next(0, 2) * 30;  // 0 sau 30

                        var startLocal = new DateTime(
                            date.Year, date.Month, date.Day,
                            hour, minute, 0,
                            DateTimeKind.Local);

                        var startUtc = startLocal.ToUniversalTime();
                        var endUtc = startUtc.AddMinutes(30);

                        var key = $"{cabinet.Id}|{startUtc:o}";
                        if (usedKeys.Contains(key))
                            continue;

                        usedKeys.Add(key);

                        var capacity = rng.Next(1, 4);
                        var booked = rng.Next(0, capacity + 1);

                        newSlots.Add(new VetTimeSlot
                        {
                            VetCabinetId = cabinet.Id,
                            StartTimeUtc = startUtc,
                            EndTimeUtc = endUtc,
                            Capacity = capacity,
                            BookedCount = booked
                        });

                        totalSlots++;
                    }
                }
            }

            if (newSlots.Count > 0)
            {
                await database.VetTimeSlots.AddRangeAsync(newSlots);
            }
        }
    }
}