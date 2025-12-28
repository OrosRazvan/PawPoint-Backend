using PawPoint.DB.Entities;
using PawPoint.Services.Services;
using Microsoft.AspNetCore.Identity;

namespace PawPoint.Tests.ServiceTests
{
    public class PasswordServiceTests
    {
        [Fact]
        public void Hash_And_Verify()
        {
            var hasher = new PasswordHasher<User>();
            var svc = new PasswordService(hasher);

            var hash = svc.Hash("P@ssw0rd!");

            Assert.True(svc.Verify("P@ssw0rd!", hash));
            Assert.False(svc.Verify("wrong", hash));
        }

        [Fact]
        public void Hash_IsSalted()
        {
            var hasher = new PasswordHasher<User>();
            var svc = new PasswordService(hasher);

            var pwd = "Val1dP@ss!";

            var h1 = svc.Hash(pwd);
            var h2 = svc.Hash(pwd);

            Assert.NotEqual(h1, h2);
        }

        [Fact]
        public void Hash_Throws_On_Null_Or_Whitespace()
        {
            var hasher = new PasswordHasher<User>();
            var svc = new PasswordService(hasher);

            var ex1 = Assert.Throws<ArgumentException>(() => svc.Hash(null!));
            Assert.Equal("password", ex1.ParamName);

            var ex2 = Assert.Throws<ArgumentException>(() => svc.Hash("   "));
            Assert.Equal("password", ex2.ParamName);
        }

        [Theory]
        [InlineData("short7!", "at least 8 chars")]
        [InlineData("alllower7!", "uppercase")]
        [InlineData("ALLUPPER7!", "lowercase")]
        [InlineData("NoDigits!!", "digit")]
        [InlineData("NoSpecial7", "special")]
        public void Hash_Throws_On_Complexity_Violations(string pwd, string expectedSnippet)
        {
            var hasher = new PasswordHasher<User>();
            var svc = new PasswordService(hasher);

            var ex = Assert.Throws<ArgumentException>(() => svc.Hash(pwd));
            Assert.Equal("password", ex.ParamName);
            Assert.Contains(expectedSnippet, ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Verify_Throws_On_Null_Args()
        {
            var hasher = new PasswordHasher<User>();
            var svc = new PasswordService(hasher);
            var hash = svc.Hash("P@ssw0rd!");

            var ex1 = Assert.Throws<ArgumentException>(() => svc.Verify(null!, hash));
            Assert.Equal("password", ex1.ParamName);

            var ex2 = Assert.Throws<ArgumentException>(() => svc.Verify("P@ssw0rd!", null!));
            Assert.Equal("storedHash", ex2.ParamName);
        }
    }
}
