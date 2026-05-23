using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.DB.Enums;
using PawPoint.Services.Interfaces;
using TaskThreading = System.Threading.Tasks.Task;

namespace PawPoint.ApiServices.Helpers
{
    public class DatabaseSeedService : IDatabaseSeedService
    {
        private readonly IServiceProvider _serviceProvider;

        public DatabaseSeedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

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
                return;
            }

            if (seedRecord == null)
            {
                seedRecord = new SeedStatus { ShouldSeedDatabase = true };
                database.SeedStatuses.Add(seedRecord);
                await database.SaveChangesAsync();
            }

            var emailIndex = _serviceProvider.GetRequiredService<IEmailIndexService>();
            var pii = _serviceProvider.GetRequiredService<IPiiEncryptionService>();
            var passwordService = _serviceProvider.GetRequiredService<IPasswordService>();

            await EnsureAdminUserAsync(database, emailIndex, pii, passwordService);
            await database.SaveChangesAsync();

            if (!seedRecord.ShouldSeedDatabase)
                return;

            await SeedNotificationPreferences(database);
            await SeedNotificationTypes(database);
            await SeedVerificationTokenTypes(database);

            await SeedVetCabinets(database);
            await database.SaveChangesAsync();

            await SeedVetServicePrices(database);
            await database.SaveChangesAsync();

            await SeedVetTimeSlots(database);
            await database.SaveChangesAsync();

            seedRecord.ShouldSeedDatabase = false;
            await database.SaveChangesAsync();
        }

        private static async TaskThreading SeedVetCabinets(Context database)
        {
            var cabinets = new List<VetCabinet>
            {
                new() { Name = "HappyPaws Clinic", Address = "Str. Mihai Eminescu 15", City = "Cluj-Napoca", PhoneNumber = "0721 111 222", Website = "happypaws.ro", Rating = 4.7, DistanceKm = 3.2 },
                new() { Name = "VetPlus Center", Address = "Str. Memorandumului 9", City = "Cluj-Napoca", PhoneNumber = "0743 883 221", Website = "vetplus.ro", Rating = 4.8, DistanceKm = 2.5 },
                new() { Name = "MiauWoof Care", Address = "Bd. Eroilor 48", City = "Cluj-Napoca", PhoneNumber = "0752 667 991", Website = "miauwow.ro", Rating = 4.9, DistanceKm = 1.7 },

                new() { Name = "Bucharest PetCare", Address = "Bd. Unirii 45", City = "București", PhoneNumber = "0722 441 100", Website = "bucharestpetcare.ro", Rating = 4.8, DistanceKm = 4.1 },
                new() { Name = "Capital Vet Clinic", Address = "Calea Victoriei 120", City = "București", PhoneNumber = "0731 882 441", Website = "capitalvet.ro", Rating = 4.6, DistanceKm = 5.3 },
                new() { Name = "Urban Tails Medical", Address = "Str. Decebal 18", City = "București", PhoneNumber = "0745 120 330", Website = "urbantails.ro", Rating = 4.7, DistanceKm = 2.9 },

                new() { Name = "TimiVet Center", Address = "Str. Alba Iulia 10", City = "Timișoara", PhoneNumber = "0726 510 210", Website = "timivet.ro", Rating = 4.5, DistanceKm = 3.7 },
                new() { Name = "PawsMed Timișoara", Address = "Bd. Revoluției 22", City = "Timișoara", PhoneNumber = "0734 700 122", Website = "pawsmedtm.ro", Rating = 4.7, DistanceKm = 2.8 },

                new() { Name = "Iași Animal Clinic", Address = "Str. Palat 7", City = "Iași", PhoneNumber = "0751 909 444", Website = "iasianimal.ro", Rating = 4.6, DistanceKm = 3.1 },
                new() { Name = "Moldova VetCare", Address = "Bd. Ștefan cel Mare 32", City = "Iași", PhoneNumber = "0748 300 991", Website = "moldovavet.ro", Rating = 4.4, DistanceKm = 4.6 },

                new() { Name = "Brașov Pet Health", Address = "Str. Lungă 55", City = "Brașov", PhoneNumber = "0729 440 220", Website = "brasovpethealth.ro", Rating = 4.8, DistanceKm = 2.2 },
                new() { Name = "Carpathian Vet", Address = "Str. Zizinului 14", City = "Brașov", PhoneNumber = "0760 231 881", Website = "carpathianvet.ro", Rating = 4.6, DistanceKm = 3.9 },

                new() { Name = "Constanța VetLife", Address = "Bd. Mamaia 88", City = "Constanța", PhoneNumber = "0732 100 880", Website = "constanta-vetlife.ro", Rating = 4.5, DistanceKm = 5.1 },
                new() { Name = "SeaSide Animal Care", Address = "Str. Mircea cel Bătrân 40", City = "Constanța", PhoneNumber = "0740 662 111", Website = "seasideanimal.ro", Rating = 4.7, DistanceKm = 2.7 },

                new() { Name = "Oradea Vet Clinic", Address = "Str. Republicii 19", City = "Oradea", PhoneNumber = "0755 332 900", Website = "oradeavet.ro", Rating = 4.6, DistanceKm = 3.4 },
                new() { Name = "Sibiu Animal Health", Address = "Str. Tribunei 11", City = "Sibiu", PhoneNumber = "0724 800 155", Website = "sibiuanimal.ro", Rating = 4.7, DistanceKm = 2.6 },
                new() { Name = "Craiova Pet Clinic", Address = "Calea București 70", City = "Craiova", PhoneNumber = "0739 920 411", Website = "craiovapet.ro", Rating = 4.4, DistanceKm = 4.8 },
                new() { Name = "Galați VetPoint", Address = "Str. Domnească 60", City = "Galați", PhoneNumber = "0741 778 220", Website = "galativetpoint.ro", Rating = 4.5, DistanceKm = 3.5 }
            };

            var existingNames = await database.VetCabinets
                .Select(c => c.Name)
                .ToListAsync();

            var toInsert = cabinets
                .Where(c => !existingNames.Contains(c.Name))
                .ToList();

            if (toInsert.Count > 0)
                await database.VetCabinets.AddRangeAsync(toInsert);
        }

        private static async TaskThreading SeedVetServicePrices(Context database)
        {
            var cabinets = await database.VetCabinets.ToListAsync();

            foreach (var cabinet in cabinets)
            {
                var modifier = cabinet.City switch
                {
                    "București" => 1.25m,
                    "Cluj-Napoca" => 1.15m,
                    "Timișoara" => 1.10m,
                    "Constanța" => 1.10m,
                    "Iași" => 1.05m,
                    "Brașov" => 1.05m,
                    _ => 1.00m
                };

                decimal P(decimal basePrice) => Math.Round(basePrice * modifier, 2);

                var services = new List<VetServicePrice>
                {
                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Consultation",
                        Price = P(16 + (cabinet.Id % 8)),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Deworming",
                        DewormingType = DewormingTypeEnum.Internal,
                        Price = P(12),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Deworming",
                        DewormingType = DewormingTypeEnum.External,
                        Price = P(15),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Deworming",
                        DewormingType = DewormingTypeEnum.Combined,
                        Price = P(22),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Deworming",
                        DewormingType = DewormingTypeEnum.Control,
                        Price = P(8),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Vaccination",
                        VaccineType = VaccineType.Rabies,
                        Price = P(25),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Vaccination",
                        VaccineType = VaccineType.DHPPi,
                        Price = P(35),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Vaccination",
                        VaccineType = VaccineType.Leptospirosis,
                        Price = P(30),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Vaccination",
                        VaccineType = VaccineType.Bordetella,
                        Price = P(32),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Vaccination",
                        VaccineType = VaccineType.LymeDisease,
                        Price = P(36),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Vaccination",
                        VaccineType = VaccineType.CanineInfluenza,
                        Price = P(34),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Vaccination",
                        VaccineType = VaccineType.FelineTrivalent,
                        Price = P(33),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Vaccination",
                        VaccineType = VaccineType.FeLV,
                        Price = P(38),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Vaccination",
                        VaccineType = VaccineType.FIV,
                        Price = P(40),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Vaccination",
                        VaccineType = VaccineType.FelineChlamydia,
                        Price = P(31),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Vaccination",
                        VaccineType = VaccineType.Myxomatosis,
                        Price = P(20),
                        Currency = Currency.Eur
                    },

                    new()
                    {
                        VetCabinetId = cabinet.Id,
                        ServiceType = "Vaccination",
                        VaccineType = VaccineType.RHD,
                        Price = P(22),
                        Currency = Currency.Eur
                    }
                };

                foreach (var service in services)
                {
                    var exists = await database.VetServicePrices.AnyAsync(x =>
                        x.VetCabinetId == service.VetCabinetId &&
                        x.ServiceType == service.ServiceType &&
                        x.DewormingType == service.DewormingType &&
                        x.VaccineType == service.VaccineType);

                    if (!exists)
                    {
                        database.VetServicePrices.Add(service);
                    }
                }
            }

            await database.SaveChangesAsync();
        }

        private static async TaskThreading SeedVetTimeSlots(Context database)
        {
            const int MaxTotalSlots = 200;

            var existingCount = await database.VetTimeSlots.CountAsync();
            if (existingCount >= MaxTotalSlots)
                return;

            var cabinets = await database.VetCabinets.ToListAsync();
            if (cabinets.Count == 0)
                return;

            var rng = new Random();
            var nowDate = DateTime.UtcNow.Date;
            var endDate = nowDate.AddMonths(1);

            var newSlots = new List<VetTimeSlot>();
            var usedKeys = new HashSet<string>();

            var totalSlots = existingCount;

            for (var date = nowDate; date < endDate && totalSlots < MaxTotalSlots; date = date.AddDays(1))
            {
                foreach (var cabinet in cabinets)
                {
                    if (totalSlots >= MaxTotalSlots)
                        break;

                    if (rng.NextDouble() >= 0.5)
                        continue;

                    var slotsPerDay = rng.Next(1, 4);

                    for (int i = 0; i < slotsPerDay && totalSlots < MaxTotalSlots; i++)
                    {
                        var hour = rng.Next(8, 18);
                        var minute = rng.Next(0, 2) * 30;

                        var startLocal = new DateTime(
                            date.Year,
                            date.Month,
                            date.Day,
                            hour,
                            minute,
                            0,
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
                await database.VetTimeSlots.AddRangeAsync(newSlots);
        }

        private static async TaskThreading SeedNotificationPreferences(Context database)
        {
            var notificationPreferences = new List<NotificationPreference>
            {
                new NotificationPreference { Name = "All" },
                new NotificationPreference { Name = "InvitesOnly" },
                new NotificationPreference { Name = "Mute" }
            };

            await database.NotificationPreferences.AddRangeAsync(notificationPreferences);
        }

        private static async TaskThreading SeedVerificationTokenTypes(Context database)
        {
            var verificationTokenTypes = new List<VerificationTokenType>
            {
                new VerificationTokenType { Name = "ForgotPasswordToken" },
                new VerificationTokenType { Name = "EmailVerificationToken" }
            };

            await database.VerificationTokenTypes.AddRangeAsync(verificationTokenTypes);
        }

        private static async TaskThreading SeedNotificationTypes(Context database)
        {
            if (await database.NotificationTypes.AnyAsync())
                return;

            var notificationTypes = new List<NotificationType>
            {
                new NotificationType { Id = 1, Name = "AppointmentBooked" },
                new NotificationType { Id = 2, Name = "AppointmentReminder" },
                new NotificationType { Id = 3, Name = "AppointmentRescheduled" },
                new NotificationType { Id = 4, Name = "AppointmentCancelled" },

                new NotificationType { Id = 10, Name = "VaccinationBooked" },
                new NotificationType { Id = 11, Name = "VaccinationReminder" },
                new NotificationType { Id = 12, Name = "VaccinationDue" },
                new NotificationType { Id = 13, Name = "VaccinationUpdated" },
                new NotificationType { Id = 14, Name = "VaccinationCancelled" },

                new NotificationType { Id = 20, Name = "DewormingBooked" },
                new NotificationType { Id = 21, Name = "DewormingReminder" },
                new NotificationType { Id = 22, Name = "DewormingDue" },
                new NotificationType { Id = 23, Name = "DewormingUpdated" },
                new NotificationType { Id = 24, Name = "DewormingCancelled" },

                new NotificationType { Id = 30, Name = "FeedingReminder" },

                new NotificationType { Id = 40, Name = "AnimalCreated" },
                new NotificationType { Id = 41, Name = "AnimalUpdated" },
                new NotificationType { Id = 42, Name = "AnimalDeleted" },

                new NotificationType { Id = 50, Name = "ProfileUpdated" },
                new NotificationType { Id = 51, Name = "PasswordChanged" },
                new NotificationType { Id = 52, Name = "SettingsUpdated" },

                new NotificationType { Id = 60, Name = "ContactMessageReceived" },
                new NotificationType { Id = 61, Name = "ContactMessageReplyReceived" }
            };

            await database.NotificationTypes.AddRangeAsync(notificationTypes);
            await database.SaveChangesAsync();
        }

        private static async TaskThreading EnsureAdminUserAsync(
            Context database,
            IEmailIndexService emailIndex,
            IPiiEncryptionService pii,
            IPasswordService passwordService)
        {
            const string adminEmail = "admin@pawpoint.local";
            const string adminPassword = "Admin123!";
            const string adminFullName = "PawPoint Admin";

            var normalizedEmail = emailIndex.Normalize(adminEmail);
            var emailHash = emailIndex.ComputeHash(normalizedEmail);

            var existingAdmin = await database.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.EmailHash == emailHash);

            if (existingAdmin is not null)
            {
                if (existingAdmin.Role != UserRoleEnum.Admin)
                {
                    existingAdmin.Role = UserRoleEnum.Admin;
                    existingAdmin.IsEmailConfirmed = true;
                    existingAdmin.IsDeleted = false;
                    existingAdmin.UpdatedAt = DateTime.UtcNow;
                    await database.SaveChangesAsync();
                }

                return;
            }

            var defaultPreferenceId = await database.NotificationPreferences
                .Where(x => x.Name == "All")
                .Select(x => x.Id)
                .FirstOrDefaultAsync();

            if (defaultPreferenceId == 0)
            {
                var pref = new NotificationPreference
                {
                    Name = "All"
                };

                database.NotificationPreferences.Add(pref);
                await database.SaveChangesAsync();
                defaultPreferenceId = pref.Id;
            }

            var adminUser = new User
            {
                Email = pii.Encrypt(normalizedEmail),
                EmailHash = emailHash,
                PasswordHash = passwordService.Hash(adminPassword),
                FullName = adminFullName,
                Role = UserRoleEnum.Admin,
                IsEmailConfirmed = true,
                IsDeleted = false,
                NotificationPreferenceId = defaultPreferenceId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            database.Users.Add(adminUser);
            await database.SaveChangesAsync();
        }
    }
}