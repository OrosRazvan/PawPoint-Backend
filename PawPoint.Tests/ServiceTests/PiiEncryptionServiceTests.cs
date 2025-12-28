using PawPoint.Common.Helpers;
using PawPoint.Services.Services;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace PawPoint.Tests.ServiceTests
{
    public class PiiEncryptionServiceTests
    {
        private const string CRYPTO_DEK_TEST = "WjZ1nJr2fchR2JhY4kdgK0q7+paeF4pZ35VSG3xqXUo=";

        private static IOptions<EncryptionSettings> BuildOpts()
            => Options.Create(new EncryptionSettings { CryptoDek = CRYPTO_DEK_TEST });

        [Fact]
        public void Encrypt_Then_Decrypt_Roundtrip()
        {
            var svc = new PiiEncryptionService(BuildOpts());
            var plain = "test@example.com";

            var enc = svc.Encrypt(plain);
            var dec = svc.Decrypt(enc);

            Assert.Equal(plain, dec);
        }

        [Fact]
        public void Encrypt_IsRandomized_ByIV()
        {
            var svc = new PiiEncryptionService(BuildOpts());
            var plain = "same@value.com";

            var c1 = svc.Encrypt(plain);
            var c2 = svc.Encrypt(plain);

            Assert.NotEqual(c1, c2);
        }

        [Fact]
        public void Encrypt_Throws_On_Null_Whitespace_Or_TooLong()
        {
            var svc = new PiiEncryptionService(BuildOpts());

            var ex1 = Assert.Throws<ArgumentException>(() => svc.Encrypt(null!));
            Assert.Equal("plaintext", ex1.ParamName);

            var ex2 = Assert.Throws<ArgumentException>(() => svc.Encrypt("   "));
            Assert.Equal("plaintext", ex2.ParamName);

            var tooLong = new string('x', 8001);
            var ex3 = Assert.Throws<ArgumentException>(() => svc.Encrypt(tooLong));
            Assert.Equal("plaintext", ex3.ParamName);
        }

        [Fact]
        public void Decrypt_Fails_On_Tamper()
        {
            var svc = new PiiEncryptionService(BuildOpts());
            var enc = svc.Encrypt("x@y.z");

            var bytes = Convert.FromBase64String(enc);
            bytes[^1] ^= 0xFF;
            var tampered = Convert.ToBase64String(bytes);

            Assert.Throws<AuthenticationTagMismatchException>(() => svc.Decrypt(tampered));
        }

        [Fact]
        public void Decrypt_Throws_On_Null_Whitespace_Or_NotBase64()
        {
            var svc = new PiiEncryptionService(BuildOpts());

            var ex1 = Assert.Throws<ArgumentException>(() => svc.Decrypt(null!));
            Assert.Equal("payloadBase64", ex1.ParamName);

            var ex2 = Assert.Throws<ArgumentException>(() => svc.Decrypt("   "));
            Assert.Equal("payloadBase64", ex2.ParamName);

            var ex3 = Assert.Throws<ArgumentException>(() => svc.Decrypt("not-base64!!"));
            Assert.Equal("payloadBase64", ex3.ParamName);
        }

        [Fact]
        public void Payload_Has_Min_Length()
        {
            var svc = new PiiEncryptionService(BuildOpts());
            var enc = svc.Encrypt("abc");
            var packed = Convert.FromBase64String(enc);

            Assert.True(packed.Length >= 28);
        }
    }
}
