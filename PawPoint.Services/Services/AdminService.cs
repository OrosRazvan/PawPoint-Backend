using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;
using System.Reflection;

namespace PawPoint.Services.Services
{
    public sealed class AdminService(
        Context db,
        IPiiEncryptionService pii,
        IEmailIndexService emailIndex,
        IPasswordService passwordService) : IAdminService
    {
        private readonly Context _db = db;
        private readonly IPiiEncryptionService _pii = pii;
        private readonly IEmailIndexService _emailIndex = emailIndex;
        private readonly IPasswordService _passwordService = passwordService;

        public async Task<AdminDashboardResponse> GetDashboardAsync()
        {
            var totalUsers = await _db.Users.IgnoreQueryFilters().CountAsync();
            var activeUsers = await _db.Users.CountAsync();
            var deletedUsers = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.IsDeleted);
            var totalAnimals = await _db.Animals.IgnoreQueryFilters().CountAsync();
            var totalAppointments = await _db.Appointments.CountAsync();
            var totalVetCabinets = await _db.VetCabinets.CountAsync();

            return new AdminDashboardResponse(
                TotalUsers: totalUsers,
                ActiveUsers: activeUsers,
                DeletedUsers: deletedUsers,
                TotalAnimals: totalAnimals,
                TotalAppointments: totalAppointments,
                TotalVetCabinets: totalVetCabinets
            );
        }

        public async Task<IReadOnlyList<AdminUserItemResponse>> GetUsersAsync()
        {
            var users = await _db.Users
                .IgnoreQueryFilters()
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return users
                .Select(u => new AdminUserItemResponse(
                    Id: u.Id,
                    FullName: u.FullName,
                    Email: DecryptOrRaw(u.Email),
                    Role: ReadRole(u),
                    IsEmailConfirmed: u.IsEmailConfirmed,
                    IsDeleted: u.IsDeleted,
                    CreatedAt: u.CreatedAt
                ))
                .ToList();
        }

        public async Task<AdminUserDetailsResponse> GetUserDetailsAsync(int userId)
        {
            ValidateUserId(userId);

            var user = await _db.Users
                .IgnoreQueryFilters()
                .Include(u => u.NotificationPreference)
                .Include(u => u.Settings)
                .Include(u => u.Animals)
                    .ThenInclude(a => a.Vaccinations)
                .Include(u => u.Animals)
                    .ThenInclude(a => a.Dewormings)
                .Include(u => u.Animals)
                    .ThenInclude(a => a.Feedings)
                .Include(u => u.Animals)
                    .ThenInclude(a => a.Appointments)
                .AsSplitQuery()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                throw new InvalidOperationException("User not found.");

            var userDict = ToScalarDictionary(user, nameof(User.PasswordHash));
            userDict["Email"] = DecryptOrRaw(user.Email);
            userDict["Role"] = ReadRole(user);
            userDict["NotificationPreference"] = user.NotificationPreference?.Name;

            var settingsDict = user.Settings is null
                ? null
                : ToScalarDictionary(user.Settings);

            var animals = user.Animals
            .OrderByDescending(a => GetScalarDate(a, "CreatedAt"))
            .Select(a => new AdminAnimalDetailsResponse(
                Animal: ToScalarDictionary(a),
                Vaccinations: a.Vaccinations
                    .OrderByDescending(v => GetScalarDate(v, "CreatedAt"))
                    .Select(v => ToScalarDictionary(v))
                    .ToList(),
                Dewormings: a.Dewormings
                    .OrderByDescending(d => GetScalarDate(d, "CreatedAt"))
                    .Select(d => ToScalarDictionary(d))
                    .ToList(),
                Feedings: a.Feedings
                    .OrderByDescending(f => GetScalarDate(f, "CreatedAt"))
                    .Select(f => ToScalarDictionary(f))
                    .ToList(),
                Appointments: a.Appointments
                    .OrderByDescending(ap => GetScalarDate(ap, "CreatedAt"))
                    .Select(ap => ToScalarDictionary(ap))
                    .ToList()
            ))
            .ToList();

            return new AdminUserDetailsResponse(
                User: userDict,
                Settings: settingsDict,
                Animals: animals
            );
        }

        public async Task UpdateUserProfileAsync(int userId, AdminUpdateUserProfileRequest request)
        {
            ValidateUserId(userId);
            ValidateProfileRequest(request);

            var user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                throw new InvalidOperationException("User not found.");

            var normalizedEmail = _emailIndex.Normalize(request.Email);
            var emailHash = _emailIndex.ComputeHash(normalizedEmail);

            var duplicateExists = await _db.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Id != userId && u.EmailHash == emailHash);

            if (duplicateExists)
                throw new InvalidOperationException("Another user already uses this email.");

            user.FullName = ValidateFullName(request.FullName);
            user.Email = _pii.Encrypt(normalizedEmail);
            user.EmailHash = emailHash;
            user.PhoneNumber = NormalizePhone(request.PhoneNumber);
            user.IsEmailConfirmed = request.IsEmailConfirmed;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public async Task SetUserPasswordAsync(int userId, AdminSetUserPasswordRequest request)
        {
            ValidateUserId(userId);
            ValidatePasswordRequest(request);

            var user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                throw new InvalidOperationException("User not found.");

            user.PasswordHash = _passwordService.Hash(request.NewPassword.Trim());
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public async Task UpdateUserSettingsAsync(int userId, AdminUpdateUserSettingsRequest request)
        {
            ValidateUserId(userId);

            var user = await _db.Users
                .IgnoreQueryFilters()
                .Include(u => u.Settings)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                throw new InvalidOperationException("User not found.");

            var prefExists = await _db.NotificationPreferences
                .AnyAsync(p => p.Id == request.NotificationPreferenceId);

            if (!prefExists)
                throw new InvalidOperationException("Notification preference not found.");

            user.NotificationPreferenceId = request.NotificationPreferenceId;

            if (user.Settings is null)
            {
                user.Settings = new UserSettings
                {
                    UserId = user.Id
                };
            }

            user.Settings.DarkMode = request.DarkMode;
            user.Settings.TextSize = request.TextSize;
            user.Settings.WeightUnit = request.WeightUnit;
            user.Settings.DateFormat = request.DateFormat;
            user.Settings.EnableNotifications = request.EnableNotifications;
            user.Settings.VaccinationNotifications = request.VaccinationNotifications;
            user.Settings.AppointmentNotifications = request.AppointmentNotifications;
            user.Settings.DewormingNotifications = request.DewormingNotifications;
            user.Settings.NotificationBadgeMode = request.NotificationBadgeMode;

            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public async Task RestoreUserAsync(int userId)
        {
            ValidateUserId(userId);

            var user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                throw new InvalidOperationException("User not found.");

            user.IsDeleted = false;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public async Task SoftDeleteUserAsync(int userId)
        {
            ValidateUserId(userId);

            var user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                throw new InvalidOperationException("User not found.");

            user.IsDeleted = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        private static void ValidateUserId(int userId)
        {
            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId), "User id must be a positive integer.");
        }

        private static void ValidateProfileRequest(AdminUpdateUserProfileRequest request)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.FullName))
                throw new ArgumentException("Full name is required.", nameof(request.FullName));

            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Email is required.", nameof(request.Email));

            if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out _))
                throw new ArgumentException("Email format is invalid.", nameof(request.Email));
        }

        private static void ValidatePasswordRequest(AdminSetUserPasswordRequest request)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                throw new ArgumentException("New password is required.", nameof(request.NewPassword));

            if (request.NewPassword.Trim().Length < 8)
                throw new ArgumentException("New password must be at least 8 characters.", nameof(request.NewPassword));
        }

        private static string ValidateFullName(string fullName)
        {
            var name = fullName.Trim();

            if (name.Length == 0)
                throw new ArgumentException("Full name is required.", nameof(fullName));

            if (name.Length > 100)
                throw new ArgumentOutOfRangeException(nameof(fullName), "Full name must be <= 100 characters.");

            return name;
        }

        private static string? NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            var trimmed = phone.Trim();
            if (trimmed.Length > 32)
                throw new ArgumentOutOfRangeException(nameof(phone), "Phone number too long.");

            return trimmed;
        }

        private string DecryptOrRaw(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            try
            {
                return _pii.Decrypt(value);
            }
            catch
            {
                return value;
            }
        }

        private static string ReadRole(User user)
        {
            var roleProp = typeof(User).GetProperty("Role");
            var value = roleProp?.GetValue(user);
            return value?.ToString() ?? "User";
        }

        private static Dictionary<string, object?> ToScalarDictionary(object entity, params string[] excludedNames)
        {
            var excluded = new HashSet<string>(excludedNames, StringComparer.OrdinalIgnoreCase);

            return entity.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .Where(p => !excluded.Contains(p.Name))
                .Where(p => IsScalarLike(p.PropertyType))
                .ToDictionary(
                    p => p.Name,
                    p =>
                    {
                        var value = p.GetValue(entity);
                        return value is Enum ? value.ToString() : value;
                    });
        }

        private static bool IsScalarLike(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;

            return t.IsPrimitive
                || t.IsEnum
                || t == typeof(string)
                || t == typeof(decimal)
                || t == typeof(DateTime)
                || t == typeof(DateTimeOffset)
                || t == typeof(Guid)
                || t == typeof(TimeSpan)
                || t == typeof(bool);
        }

        private static DateTime GetScalarDate(object entity, string propertyName)
        {
            var prop = entity.GetType().GetProperty(propertyName);
            var value = prop?.GetValue(entity);

            if (value is DateTime dt)
                return dt;

            return DateTime.MinValue;
        }
    }
}