using PawPoint.Services.Services;

namespace PawPoint.Tests.ServiceTests
{
    public class EmailIndexServiceTests
    {
        private readonly EmailIndexService _svc = new();

        [Theory]
        [InlineData("A@B.Com", "a@b.com")]
        [InlineData("  a@b.com  ", "a@b.com")]
        public void Normalize_Works(string input, string expected)
        {
            var norm = _svc.Normalize(input);
            Assert.Equal(expected, norm);
        }

        [Fact]
        public void Normalize_Throws_On_Null_Or_Whitespace()
        {
            var ex1 = Assert.Throws<ArgumentException>(() => _svc.Normalize(null!));
            Assert.Equal("email", ex1.ParamName);

            var ex2 = Assert.Throws<ArgumentException>(() => _svc.Normalize("   "));
            Assert.Equal("email", ex2.ParamName);
        }

        [Fact]
        public void ComputeHash_IsDeterministic_AndHex64()
        {
            var n1 = _svc.Normalize("  A@B.Com ");
            var h1 = _svc.ComputeHash(n1);
            var h2 = _svc.ComputeHash("a@b.com");

            Assert.Equal(h1, h2);
            Assert.Matches("^[0-9a-f]{64}$", h1);
        }

        [Fact]
        public void ComputeHash_Throws_On_Null_Or_Empty()
        {
            var ex1 = Assert.Throws<ArgumentException>(() => _svc.ComputeHash(null!));
            Assert.Equal("normalizedEmail", ex1.ParamName);

            var ex2 = Assert.Throws<ArgumentException>(() => _svc.ComputeHash(""));
            Assert.Equal("normalizedEmail", ex2.ParamName);

            var ex3 = Assert.Throws<ArgumentException>(() => _svc.ComputeHash("   "));
            Assert.Equal("normalizedEmail", ex3.ParamName);
        }
    }
}
