using PawPoint.DB.Entities;
using PawPoint.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace PawPoint.Services.Services
{
    public sealed class PasswordService(IPasswordHasher<User> hasher) : IPasswordService
    {
        private readonly IPasswordHasher<User> _hasher = hasher;

        public string Hash(string password)
        {
            ValidatePassword(password);
            return _hasher.HashPassword(null!, password);
        }

        public bool Verify(string password, string storedHash)
        {
            _ = password ?? throw new ArgumentException("Password cannot be null.", nameof(password));
            _ = storedHash ?? throw new ArgumentException("Hash cannot be null.", nameof(storedHash));
            var res = _hasher.VerifyHashedPassword(null!, storedHash, password);
            return res is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }

        private static void ValidatePassword(string password)
        {
            var hasUpper = password is not null && Regex.IsMatch(password, @"[A-Z]");
            var hasLower = password is not null && Regex.IsMatch(password, @"[a-z]");
            var hasDigit = password is not null && Regex.IsMatch(password, @"\d");
            var hasSpecial = password is not null && Regex.IsMatch(password, @"[^A-Za-z0-9]");
            var length = password?.Length ?? 0;
            var isBlank = string.IsNullOrWhiteSpace(password);

            _ = (password, isBlank, length, hasUpper, hasLower, hasDigit, hasSpecial) switch
            {
                (null, _, _, _, _, _, _) => throw new ArgumentException("Password cannot be null.", nameof(password)),
                (_, true, _, _, _, _, _) => throw new ArgumentException("Password cannot be empty.", nameof(password)),
                (_, _, < 8, _, _, _, _) => throw new ArgumentException("Password must be at least 8 chars.", nameof(password)),
                (_, _, _, false, _, _, _) => throw new ArgumentException("Password must contain an uppercase letter.", nameof(password)),
                (_, _, _, _, false, _, _) => throw new ArgumentException("Password must contain a lowercase letter.", nameof(password)),
                (_, _, _, _, _, false, _) => throw new ArgumentException("Password must contain a digit.", nameof(password)),
                (_, _, _, _, _, _, false) => throw new ArgumentException("Password must contain a special char.", nameof(password)),
                _ => true
            };
        }
    }
}
