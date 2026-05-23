using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;
using System.Linq;

namespace PawPoint.Services.Services
{
    public sealed class UserSettingsService(Context db) : IUserSettingsService
    {
        private readonly Context _db = db;

        private static readonly HashSet<string> AllowedTextSizes = new(StringComparer.OrdinalIgnoreCase)
        { "Small", "Medium", "Large" };

        private static readonly HashSet<string> AllowedWeightUnits = new(StringComparer.OrdinalIgnoreCase)
        { "kg", "lb" };

        private static readonly HashSet<string> AllowedDateFormats = new(StringComparer.OrdinalIgnoreCase)
        { "DD/MM/YYYY", "MM/DD/YYYY", "YYYY-MM-DD" };

        private static readonly HashSet<string> AllowedCurrencies = new(StringComparer.OrdinalIgnoreCase)
        { "EUR", "RON" };

        private static readonly HashSet<string> AllowedBadgeModes = new(StringComparer.OrdinalIgnoreCase)
        { "count", "dot" };

        public async Task<UserSettingsResponse> GetAsync(int userId)
        {
            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId));

            var settings = await _db.UserSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (settings is null)
            {
                settings = new UserSettings
                {
                    UserId = userId,
                    Currency = "EUR"
                };

                _db.UserSettings.Add(settings);
                await _db.SaveChangesAsync();
            }

            return Map(settings);
        }

        public async Task<UserSettingsResponse> UpdateAsync(
            int userId,
            UserSettingsUpdateRequest request)
        {
            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId));

            if (request is null)
                throw new ArgumentNullException(nameof(request));

            var settings = await _db.UserSettings
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (settings is null)
            {
                settings = new UserSettings
                {
                    UserId = userId,
                    Currency = "EUR"
                };

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

            if (request.Currency is not null)
            {
                var v = request.Currency.Trim().ToUpperInvariant();

                if (!AllowedCurrencies.Contains(v))
                    throw new ArgumentException("Currency must be EUR or RON.");

                settings.Currency = v;
            }

            if (request.NotificationBadgeMode is not null)
            {
                var v = request.NotificationBadgeMode.Trim();

                if (!AllowedBadgeModes.Contains(v))
                    throw new ArgumentException("NotificationBadgeMode must be 'count' or 'dot'.");

                settings.NotificationBadgeMode = v.ToLowerInvariant();
            }

            if (request.EnableNotifications.HasValue)
            {
                settings.EnableNotifications = request.EnableNotifications.Value;

                if (!settings.EnableNotifications)
                {
                    settings.VaccinationNotifications = false;
                    settings.AppointmentNotifications = false;
                    settings.DewormingNotifications = false;
                }
            }

            if (settings.EnableNotifications)
            {
                if (request.VaccinationNotifications.HasValue)
                    settings.VaccinationNotifications = request.VaccinationNotifications.Value;

                if (request.AppointmentNotifications.HasValue)
                    settings.AppointmentNotifications = request.AppointmentNotifications.Value;

                if (request.DewormingNotifications.HasValue)
                    settings.DewormingNotifications = request.DewormingNotifications.Value;
            }

            await _db.SaveChangesAsync();

            return Map(settings);
        }

        private static string NormalizeTextSize(string v)
            => v.Equals("small", StringComparison.OrdinalIgnoreCase) ? "Small"
             : v.Equals("large", StringComparison.OrdinalIgnoreCase) ? "Large"
             : "Medium";

        private static UserSettingsResponse Map(UserSettings s)
        => new(
            UserId: s.UserId,
            DarkMode: s.DarkMode,
            Currency: string.IsNullOrWhiteSpace(s.Currency) ? "EUR" : s.Currency,
            TextSize: string.IsNullOrWhiteSpace(s.TextSize) ? "Medium" : s.TextSize,
            WeightUnit: string.IsNullOrWhiteSpace(s.WeightUnit) ? "kg" : s.WeightUnit,
            DateFormat: string.IsNullOrWhiteSpace(s.DateFormat) ? "DD/MM/YYYY" : s.DateFormat,
            NotificationPreferenceId: 0,
            NotificationPreference: "All",
            EnableNotifications: s.EnableNotifications,
            VaccinationNotifications: s.VaccinationNotifications,
            AppointmentNotifications: s.AppointmentNotifications,
            DewormingNotifications: s.DewormingNotifications,
            NotificationBadgeMode: string.IsNullOrWhiteSpace(s.NotificationBadgeMode)
                ? "count"
                : s.NotificationBadgeMode
        );
    }
}