using PawPoint.Common.Helpers;
using PawPoint.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace PawPoint.Services.Services
{
    public class PiiEncryptionService(IOptions<EncryptionSettings> options) : IPiiEncryptionService
    {
        private readonly byte[] _dek = InitDek(options);

        private static byte[] InitDek(IOptions<EncryptionSettings> options)
        {
            var dekB64 = options?.Value?.CryptoDek
                         ?? throw new InvalidOperationException("Encryption:CryptoDek missing");
            var bytes = Convert.FromBase64String(dekB64);
            if (bytes.Length != 32)
                throw new InvalidOperationException("Encryption:CryptoDek must decode to 32 bytes.");
            return bytes;
        }

        public string Encrypt(string plaintext)
        {
            ValidateEncryptInput(plaintext);

            var iv = RandomNumberGenerator.GetBytes(12);
            var plain = Encoding.UTF8.GetBytes(plaintext);
            var cipher = new byte[plain.Length];
            var tag = new byte[16];

            using var gcm = new AesGcm(_dek, tag.Length);
            gcm.Encrypt(iv, plain, cipher, tag);

            var packed = new byte[iv.Length + tag.Length + cipher.Length];
            Buffer.BlockCopy(iv, 0, packed, 0, iv.Length);
            Buffer.BlockCopy(tag, 0, packed, iv.Length, tag.Length);
            Buffer.BlockCopy(cipher, 0, packed, iv.Length + tag.Length, cipher.Length);
            return Convert.ToBase64String(packed);
        }

        public string Decrypt(string payloadBase64)
        {
            ValidateDecryptInput(payloadBase64);

            var p = Convert.FromBase64String(payloadBase64);
            if (p.Length < 28) throw new ArgumentException("Payload too short.", nameof(payloadBase64));

            var iv = p.AsSpan(0, 12).ToArray();
            var tag = p.AsSpan(12, 16).ToArray();
            var c = p.AsSpan(28).ToArray();

            using var gcm = new AesGcm(_dek, tag.Length);
            var plain = new byte[c.Length];
            gcm.Decrypt(iv, c, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }

        private static void ValidateEncryptInput(string plaintext)
        {
            _ = (plaintext, IsBlank: string.IsNullOrWhiteSpace(plaintext), Len: plaintext?.Length ?? 0) switch
            {
                (null, _, _) => throw new ArgumentException("Plaintext cannot be null.", nameof(plaintext)),
                (_, true, _) => throw new ArgumentException("Plaintext cannot be empty or whitespace.", nameof(plaintext)),
                (_, _, > 8000) => throw new ArgumentException("Plaintext too long (max 8000 chars).", nameof(plaintext)),
                _ => true
            };
        }

        private static void ValidateDecryptInput(string payloadBase64)
        {
            static bool IsBase64String(string s)
            {
                try { Convert.FromBase64String(s); return true; }
                catch { return false; }
            }

            var isBlank = string.IsNullOrWhiteSpace(payloadBase64);
            var isBase64 = payloadBase64 is not null && IsBase64String(payloadBase64);

            _ = (payloadBase64, isBlank, isBase64) switch
            {
                (null, _, _) => throw new ArgumentException("Payload cannot be null.", nameof(payloadBase64)),
                (_, true, _) => throw new ArgumentException("Payload cannot be empty or whitespace.", nameof(payloadBase64)),
                (_, _, false) => throw new ArgumentException("Payload is not valid Base64.", nameof(payloadBase64)),
                _ => true
            };
        }
    }
}
