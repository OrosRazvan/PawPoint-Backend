using PawPoint.Common.Helpers;
using PawPoint.Services.Services;
using Microsoft.Extensions.Options;

namespace PawPoint.Tests.ServiceTests
{
    public class PhoneEncryptionTests
    {
        private const string CRYPTO_DEK_TEST = "WjZ1nJr2fchR2JhY4kdgK0q7+paeF4pZ35VSG3xqXUo=";

        private static IOptions<EncryptionSettings> BuildOpts()
            => Options.Create(new EncryptionSettings { CryptoDek = CRYPTO_DEK_TEST });

        [Fact]
        public void Null_Phone_Is_Ok()
        {
            string? phone = null;
            Assert.Null(phone);
        }

        [Fact]
        public void Encrypt_Decrypt_Phone()
        {
            var svc = new PiiEncryptionService(BuildOpts());
            var plain = "+40722123456";

            var enc = svc.Encrypt(plain);
            var dec = svc.Decrypt(enc);

            Assert.Equal(plain, dec);
            Assert.True(enc.Length > 20);
        }
    }
}
