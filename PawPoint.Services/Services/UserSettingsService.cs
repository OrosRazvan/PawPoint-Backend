using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Services
{
    public sealed class UserSettingsService(Context db) : IUserSettingsService
    {
        private readonly Context _db = db;

        // valori permise
        private static readonly HashSet<string> AllowedTextSizes = new(StringComparer.OrdinalIgnoreCase)
        { "Small", "Medium", "Large" };

        private static readonly HashSet<string> AllowedWeightUnits = new(StringComparer.OrdinalIgnoreCase)
        { "kg", "lb" };

        // ține-l simplu acum; poți extinde ulterior
        private static readonly HashSet<string> AllowedDateFormats = new(StringComparer.OrdinalIgnoreCase)
        { "DD/MM/YYYY", "MM/DD/YYYY", "YYYY-MM-DD" };

        public async Task<UserSettingsResponse> GetAsync(int userId)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));

            var settings = await _db.UserSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            // dacă nu există, creează default (auto-heal)
            if (settings is null)
            {
                settings = new UserSettings { UserId = userId };
                _db.UserSettings.Add(settings);
                await _db.SaveChangesAsync();
            }

            return Map(settings);
        }

        public async Task<UserSettingsResponse> UpdateAsync(int userId, UserSettingsUpdateRequest request)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (request is null) throw new ArgumentNullException(nameof(request));

            var settings = await _db.UserSettings
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (settings is null)
            {
                settings = new UserSettings { UserId = userId };
                _db.UserSettings.Add(settings);
            }

            if (request.DarkMode.HasValue)
                settings.DarkMode = request.DarkMode.Value;

            if (request.TextSize is not null)
            {
                var v = request.TextSize.Trim();
                if (!AllowedTextSizes.Contains(v))
                    throw new ArgumentException("TextSize must be one of: Small, Medium, Large.");
                settings.TextSize = NormalizeTextSize(v);
            }

            if (request.WeightUnit is not null)
            {
                var v = request.WeightUnit.Trim();
                if (!AllowedWeightUnits.Contains(v))
                    throw new ArgumentException("WeightUnit must be 'kg' or 'lb'.");
                settings.WeightUnit = v.ToLowerInvariant();
            }

            if (request.DateFormat is not null)
            {
                var v = request.DateFormat.Trim();
                if (!AllowedDateFormats.Contains(v))
                    throw new ArgumentException("DateFormat must be one of: DD/MM/YYYY, MM/DD/YYYY, YYYY-MM-DD.");
                settings.DateFormat = v.ToUpperInvariant();
            }

            await _db.SaveChangesAsync();

            // return fresh
            return Map(settings);
        }

        private static string NormalizeTextSize(string v)
            => v.Equals("small", StringComparison.OrdinalIgnoreCase) ? "Small"
             : v.Equals("large", StringComparison.OrdinalIgnoreCase) ? "Large"
             : "Medium";

        private static UserSettingsResponse Map(UserSettings s)
            => new(
                s.UserId,
                s.DarkMode,
                s.TextSize,
                s.WeightUnit,
                s.DateFormat,
                NotificationPreferenceId: 0,
                NotificationPreference: "All"
            );

    }
}
