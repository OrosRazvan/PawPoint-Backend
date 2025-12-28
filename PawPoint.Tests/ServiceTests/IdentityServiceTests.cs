using PawPoint.Common.Helpers;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PawPoint.Tests.ServiceTests
{
    public class IdentityServiceTests
    {
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly Mock<ITokenService> _tokenServiceMock;
        private readonly JwtSettings _jwtSettings;
        private readonly IdentityService _identityService;
        private const string TestSecretKey = "this-is-a-super-secure-test-secret-key-for-unit-tests";

        public IdentityServiceTests()
        {
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _tokenServiceMock = new Mock<ITokenService>();

            _jwtSettings = new JwtSettings
            {
                AccessSecret = TestSecretKey,
                Issuer = "test-issuer",
                Audience = "test-audience",
                AccessExpiresInMinutes = 15,
                RefreshExpiresInMinutes = 60,
                RefreshSecret = "another-test-secret-for-refreshing"
            };

            _identityService = new IdentityService(
                _httpContextAccessorMock.Object,
                _tokenServiceMock.Object
            );
        }

        private string GenerateTestToken(string userId, DateTime expires, string secret, DateTime? notBefore = null)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim("typ", "access")
                }),
                Expires = expires,
                NotBefore = notBefore,
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        [Fact]
        public void GetCurrentIdentity_WithValidTokenInHeader_ReturnsUserId()
        {
            //Arrange
            var expectedUserId = "user-123";
            var validToken = "a-valid-token-string-for-the-mock";
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, expectedUserId) };
            var identity = new ClaimsIdentity(claims);
            var expectedPrincipal = new ClaimsPrincipal(identity);
            _tokenServiceMock.Setup(s => s.ValidateAccessToken(validToken))
                             .Returns(expectedPrincipal);
            var mockHeaders = new HeaderDictionary
            {
                { "Authorization", new StringValues(validToken) }
            };
            var mockRequest = new Mock<HttpRequest>();
            mockRequest.Setup(x => x.Headers).Returns(mockHeaders);
            var mockHttpContext = new Mock<HttpContext>();
            mockHttpContext.Setup(x => x.Request).Returns(mockRequest.Object);
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

            //Act
            var actualUserId = _identityService.GetCurrentIdentity();

            //Assert
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void GetCurrentIdentity_WhenTokenIsMissing_ReturnsNull()
        {
            //Arrange
            var mockHeaders = new HeaderDictionary();
            var mockRequest = new Mock<HttpRequest>();
            mockRequest.Setup(x => x.Headers).Returns(mockHeaders);
            var mockHttpContext = new Mock<HttpContext>();
            mockHttpContext.Setup(x => x.Request).Returns(mockRequest.Object);
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

            //Act
            var result = _identityService.GetCurrentIdentity();

            //Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetCurrentIdentity_WithInvalidSignatureToken_ReturnsNull()
        {
            //Arrange
            var userId = "user-123";
            var invalidToken = GenerateTestToken(userId, DateTime.UtcNow.AddMinutes(5), "a-completely-different-and-valid-length-secret-key-for-testing");
            var mockHeaders = new HeaderDictionary { { "Authorization", new StringValues(invalidToken) } };
            var mockRequest = new Mock<HttpRequest>();
            mockRequest.Setup(x => x.Headers).Returns(mockHeaders);
            var mockHttpContext = new Mock<HttpContext>();
            mockHttpContext.Setup(x => x.Request).Returns(mockRequest.Object);
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

            //Act
            var result = _identityService.GetCurrentIdentity();

            //Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetCurrentIdentity_WithExpiredToken_ReturnsNull()
        {
            //Arrange
            var userId = "user-123";
            var expiredTime = DateTime.UtcNow.AddMinutes(-5);
            var notBeforeTime = DateTime.UtcNow.AddMinutes(-10);
            var expiredToken = GenerateTestToken(userId, expiredTime, _jwtSettings.AccessSecret, notBeforeTime);
            var mockHeaders = new HeaderDictionary { { "Authorization", new StringValues(expiredToken) } };
            var mockRequest = new Mock<HttpRequest>();
            mockRequest.Setup(x => x.Headers).Returns(mockHeaders);
            var mockHttpContext = new Mock<HttpContext>();
            mockHttpContext.Setup(x => x.Request).Returns(mockRequest.Object);
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

            //Act
            var result = _identityService.GetCurrentIdentity();

            //Assert
            Assert.Null(result);
        }
        [Fact]
        public void GetCurrentIdentity_OnSecondCall_ReturnsCachedUserIdWithoutReadingHeader()
        {
            //Arrange
            var expectedUserId = "user-123";
            var validToken = "any-valid-token-for-this-test";
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, expectedUserId) };
            var identity = new ClaimsIdentity(claims);
            var expectedPrincipal = new ClaimsPrincipal(identity);
            _tokenServiceMock.Setup(s => s.ValidateAccessToken(validToken))
                             .Returns(expectedPrincipal);
            var mockHeaders = new HeaderDictionary { { "Authorization", new StringValues(validToken) } };
            var mockRequest = new Mock<HttpRequest>();
            mockRequest.Setup(x => x.Headers).Returns(mockHeaders);
            var mockHttpContext = new Mock<HttpContext>();
            mockHttpContext.Setup(x => x.Request).Returns(mockRequest.Object);
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

            //Act
            var firstResult = _identityService.GetCurrentIdentity();
            var secondResult = _identityService.GetCurrentIdentity();

            //Assert
            Assert.Equal(expectedUserId, firstResult);
            Assert.Equal(expectedUserId, secondResult);
            _tokenServiceMock.Verify(s => s.ValidateAccessToken(It.IsAny<string>()), Times.Once());
        }

        [Theory]
        [InlineData("user-alpha-789")]
        [InlineData("user-beta-456")]
        [InlineData("another-user-!@#$")]
        public void GetCurrentIdentity_WithDistinctUserTokens_ReturnsCorrectUserIdAndDoesNotConfuseUsers(string expectedUserId)
        {
            //Arrange
            var validToken = $"a-unique-token-for-{expectedUserId}";
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, expectedUserId) };
            var identity = new ClaimsIdentity(claims);
            var expectedPrincipal = new ClaimsPrincipal(identity);
            _tokenServiceMock.Setup(s => s.ValidateAccessToken(validToken))
                             .Returns(expectedPrincipal);
            var identityService = new IdentityService(
                _httpContextAccessorMock.Object,
                _tokenServiceMock.Object
            );
            var mockHeaders = new HeaderDictionary { { "Authorization", new StringValues(validToken) } };
            var mockRequest = new Mock<HttpRequest>();
            mockRequest.Setup(x => x.Headers).Returns(mockHeaders);
            var mockHttpContext = new Mock<HttpContext>();
            mockHttpContext.Setup(x => x.Request).Returns(mockRequest.Object);
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

            //Act
            var actualUserId = identityService.GetCurrentIdentity();

            //Assert
            Assert.Equal(expectedUserId, actualUserId);
            _tokenServiceMock.Reset();
        }

        [Fact]
        public void GetCurrentIdentity_ForTwoSeparateRequests_ReturnsCorrectRespectiveUsers()
        {
            //Arrange
            var userId1 = "user-one-111";
            var token1 = "a-unique-token-for-user-one";
            var claims1 = new[] { new Claim(ClaimTypes.NameIdentifier, userId1) };
            _tokenServiceMock.Setup(s => s.ValidateAccessToken(token1))
                             .Returns(new ClaimsPrincipal(new ClaimsIdentity(claims1)));
            var mockHeaders1 = new HeaderDictionary { { "Authorization", new StringValues(token1) } };
            var mockRequest1 = new Mock<HttpRequest>();
            mockRequest1.Setup(r => r.Headers).Returns(mockHeaders1);
            var mockHttpContext1 = new Mock<HttpContext>();
            mockHttpContext1.Setup(c => c.Request).Returns(mockRequest1.Object);
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(mockHttpContext1.Object);
            var serviceForRequest1 = new IdentityService(_httpContextAccessorMock.Object, _tokenServiceMock.Object);

            // ACT & ASSERT (Request 1)
            var actualUser1 = serviceForRequest1.GetCurrentIdentity();
            Assert.Equal(userId1, actualUser1);

            // ARRANGE (Request 2 for User 2)
            var userId2 = "user-two-222";
            var token2 = "a-different-token-for-user-two";
            var claims2 = new[] { new Claim(ClaimTypes.NameIdentifier, userId2) };
            _tokenServiceMock.Setup(s => s.ValidateAccessToken(token2))
                             .Returns(new ClaimsPrincipal(new ClaimsIdentity(claims2)));

            var mockHeaders2 = new HeaderDictionary { { "Authorization", new StringValues(token2) } };
            var mockRequest2 = new Mock<HttpRequest>();
            mockRequest2.Setup(r => r.Headers).Returns(mockHeaders2);
            var mockHttpContext2 = new Mock<HttpContext>();
            mockHttpContext2.Setup(c => c.Request).Returns(mockRequest2.Object);
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(mockHttpContext2.Object);
            var serviceForRequest2 = new IdentityService(_httpContextAccessorMock.Object, _tokenServiceMock.Object);

            // ACT & ASSERT (Request 2)
            var actualUser2 = serviceForRequest2.GetCurrentIdentity();
            Assert.Equal(userId2, actualUser2);
        }
    }
}