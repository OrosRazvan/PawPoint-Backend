using PawPoint.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace PawPoint.Services.Services
{
    public class EmailIndexService : IEmailIndexService
    {
        public string Normalize(string email)
        {
            _ = (email, IsBlank: string.IsNullOrWhiteSpace(email)) switch
            {
                (null, _) => throw new ArgumentException("Email cannot be null.", nameof(email)),
                (_, true) => throw new ArgumentException("Email cannot be empty or whitespace.", nameof(email)),
                _ => true
            };

            return email.Trim().ToLowerInvariant(); 
        }

        public string ComputeHash(string normalizedEmail)
        {
            _ = (normalizedEmail, IsBlank: string.IsNullOrWhiteSpace(normalizedEmail)) switch
            {
                (null, _) => throw new ArgumentException("Normalized email cannot be null.", nameof(normalizedEmail)),
                (_, true) => throw new ArgumentException("Normalized email cannot be empty.", nameof(normalizedEmail)),
                _ => true
            };

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalizedEmail));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
