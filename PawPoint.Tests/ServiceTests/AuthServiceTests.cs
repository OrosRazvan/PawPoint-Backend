using Hangfire;
using PawPoint.Common.Helpers;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.DB.Enums;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;
using PawPoint.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using Task = System.Threading.Tasks.Task;

namespace PawPoint.Tests.ServiceTests
{
    public class AuthServiceTests
    {
        private static Context InMemoryDb()
        {
            var opts = new DbContextOptionsBuilder<Context>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new Context(opts);
        }

        private static Context SqliteInMemoryDb()
        {
            var conn = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
            conn.Open();
            var opts = new DbContextOptionsBuilder<Context>()
                .UseSqlite(conn)
                .Options;

            var ctx = new Context(opts);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        private static (AuthService svc,
                        Mock<IEmailIndexService> emailIndex,
                        Mock<IPasswordService> pwd,
                        Mock<IEmailService> emailSvc,
                        Mock<IPiiEncryptionService> pii,
                        Mock<ITokenService> tokenSvc,
                        Mock<ITemplateRenderer> tpl,
                        IOptions<VerificationTokenSettings> vtOptions,
                        Mock<IBackgroundJobClient> jobs)
            BuildService(Context db,
                         VerificationTokenSettings? vtOverride = null,
                         AppUrls? urlsOverride = null,
                         TemplateSettings? tplSettingsOverride = null)
        {
            var emailIndex = new Mock<IEmailIndexService>();
            var pwd = new Mock<IPasswordService>();
            var emailSvc = new Mock<IEmailService>();
            var pii = new Mock<IPiiEncryptionService>();
            var tokenSvc = new Mock<ITokenService>();
            var tpl = new Mock<ITemplateRenderer>();
            var jobs = new Mock<IBackgroundJobClient>();

            var urls = Options.Create(urlsOverride ?? new AppUrls
            {
                ApiBase = "https://localhost:7049",
                VerifyEmailPath = "/auth/verify-email",
                FrontendBase = "https://lateral-inspire.vercel.app/",
                FrontendLoginPath = "/login"
            });

            var vt = Options.Create(vtOverride ?? new VerificationTokenSettings
            {
                EmailExpiresInDays = 7,
                PasswordResetExpiresInMinutes = 30
            });

            var tplSettings = Options.Create(tplSettingsOverride ?? new TemplateSettings
            {
                Root = "Templates",
                Email = new TemplateSettings.EmailTemplates
                {
                    VerifyHtml = "Email/VerifyEmail.html",
                    VerifyText = "Email/VerifyEmail.txt",
                    ResetHtml = "Email/ResetPassword.cshtml",
                    ResetText = "Email/ResetPassword.txt"
                }
            });

            tpl.Setup(x => x.RenderAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
               .ReturnsAsync("TEMPLATE_BODY");

            var svc = new AuthService(
                db,
                emailIndex.Object,
                pwd.Object,
                emailSvc.Object,
                pii.Object,
                urls,
                vt,
                tokenSvc.Object,
                tpl.Object,
                tplSettings,
                jobs.Object
            );

            return (svc, emailIndex, pwd, emailSvc, pii, tokenSvc, tpl, vt, jobs);
        }

        #region Register
        [Fact]
        public async Task Register_Saves_Encrypted_Email_And_Creates_VerificationToken()
        {
            var db = InMemoryDb();
            var (svc, emailIndex, pwd, emailService, pii, _, _, vt, _) = BuildService(db);

            emailIndex.Setup(x => x.Normalize("User@Mail.com")).Returns("user@mail.com");
            emailIndex.Setup(x => x.ComputeHash("user@mail.com")).Returns("h123");
            pwd.Setup(x => x.Hash("Aa1!aaaa")).Returns("hash");
            pii.Setup(x => x.Encrypt("user@mail.com")).Returns("ENC(user@mail.com)");

            var res = await svc.RegisterAsync(new RegisterRequest("Jane Doe", "User@Mail.com", "Aa1!aaaa"));

            Assert.Equal("user@mail.com", res.Email);
            Assert.Equal("Jane Doe", res.FullName);
            Assert.False(res.EmailConfirmed);

            var u = await db.Users.SingleAsync();
            Assert.Equal("ENC(user@mail.com)", u.Email);
            Assert.Equal("h123", u.EmailHash);
            Assert.False(u.IsEmailConfirmed);

            var tok = await db.VerificationTokens.Include(v => v.VerificationTokenType).SingleAsync();
            Assert.Equal("EmailVerificationToken", tok.VerificationTokenType.Name);
            Assert.Equal(u.Id, tok.UserId);
            Assert.True(tok.ExpirationDate > DateTime.UtcNow.AddDays(vt.Value.EmailExpiresInDays - 1));
            emailService.Verify(es => es.SendEmailAsync("user@mail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Register_DuplicateEmail_Unverified_Resends_And_ThrowsUnverifiedMessage()
        {
            var db = InMemoryDb();
            db.Users.Add(new User
            {
                Email = "enc",
                EmailHash = "h123",
                PasswordHash = "x",
                FullName = "X",
                IsEmailConfirmed = false,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var (svc, emailIndex, pwd, emailSvc, pii, _, _, vt, _) = BuildService(db);
            emailIndex.Setup(x => x.Normalize("user@mail.com")).Returns("user@mail.com");
            emailIndex.Setup(x => x.ComputeHash("user@mail.com")).Returns("h123");
            pwd.Setup(x => x.Hash("Aa1!aaaa")).Returns("hash");
            pii.Setup(x => x.Encrypt("user@mail.com")).Returns("ENC(user@mail.com)");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.RegisterAsync(new RegisterRequest("Jane", "user@mail.com", "Aa1!aaaa")));

            Assert.Equal("Account exists but email is unverified. We resent the verification email.", ex.Message);

            emailSvc.Verify(es => es.SendEmailAsync("user@mail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            var tok = await db.VerificationTokens.Include(v => v.VerificationTokenType).SingleAsync();
            Assert.Equal("EmailVerificationToken", tok.VerificationTokenType.Name);
            Assert.True(tok.ExpirationDate > DateTime.UtcNow.AddDays(vt.Value.EmailExpiresInDays - 1));
        }

        [Fact]
        public async Task Register_DuplicateEmail_Unverified_ReusesUnexpired_OtherwiseCleansExpired_AndCreatesNew()
        {
            var db = InMemoryDb();

            var user = new User
            {
                Email = "enc",
                EmailHash = "h123",
                PasswordHash = "x",
                FullName = "X",
                IsEmailConfirmed = false,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var vtt = new VerificationTokenType { Name = "EmailVerificationToken" };
            db.VerificationTokenTypes.Add(vtt);

            var expired = new VerificationToken
            {
                Token = new string('d', 64),
                VerificationTokenType = vtt,
                ExpirationDate = DateTime.UtcNow.AddDays(-1),
                UserId = user.Id
            };
            var valid = new VerificationToken
            {
                Token = new string('e', 64),
                VerificationTokenType = vtt,
                ExpirationDate = DateTime.UtcNow.AddDays(5),
                UserId = user.Id
            };
            db.VerificationTokens.AddRange(expired, valid);
            await db.SaveChangesAsync();

            var (svc, emailIndex, pwd, emailSvc, pii, _, _, vt, _) = BuildService(db);
            emailIndex.Setup(x => x.Normalize("user@mail.com")).Returns("user@mail.com");
            emailIndex.Setup(x => x.ComputeHash("user@mail.com")).Returns("h123");
            pwd.Setup(x => x.Hash("Aa1!aaaa")).Returns("hash");
            pii.Setup(x => x.Encrypt("user@mail.com")).Returns("ENC(user@mail.com)");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.RegisterAsync(new RegisterRequest("Jane", "user@mail.com", "Aa1!aaaa")));

            Assert.Equal("Account exists but email is unverified. We resent the verification email.", ex.Message);

            emailSvc.Verify(es => es.SendEmailAsync("user@mail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            var tokens = await db.VerificationTokens.Include(t => t.VerificationTokenType).ToListAsync();
            Assert.Equal(2, tokens.Count);

            var reused = tokens.Single(t => t.Token == new string('e', 64));
            Assert.Equal("EmailVerificationToken", reused.VerificationTokenType.Name);
            Assert.True(reused.ExpirationDate > DateTime.UtcNow.AddDays(4));

            var stillExpired = tokens.Single(t => t.Token == new string('d', 64));
            Assert.True(stillExpired.ExpirationDate < DateTime.UtcNow);
        }

        [Fact]
        public async Task Register_DuplicateEmail_Unverified_WithOnlyExpiredTokens_CreatesFreshToken()
        {
            var db = InMemoryDb();

            var user = new User
            {
                Email = "enc",
                EmailHash = "h123",
                PasswordHash = "x",
                FullName = "X",
                IsEmailConfirmed = false,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var vtt = new VerificationTokenType { Name = "EmailVerificationToken" };
            db.VerificationTokenTypes.Add(vtt);
            db.VerificationTokens.Add(new VerificationToken
            {
                Token = new string('f', 64),
                VerificationTokenType = vtt,
                ExpirationDate = DateTime.UtcNow.AddDays(-2),
                UserId = user.Id
            });
            await db.SaveChangesAsync();

            var (svc, emailIndex, pwd, emailSvc, pii, _, _, vt, _) = BuildService(db);
            emailIndex.Setup(x => x.Normalize("user@mail.com")).Returns("user@mail.com");
            emailIndex.Setup(x => x.ComputeHash("user@mail.com")).Returns("h123");
            pwd.Setup(x => x.Hash("Aa1!aaaa")).Returns("hash");
            pii.Setup(x => x.Encrypt("user@mail.com")).Returns("ENC(user@mail.com)");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.RegisterAsync(new RegisterRequest("Jane", "user@mail.com", "Aa1!aaaa")));

            Assert.Equal("Account exists but email is unverified. We resent the verification email.", ex.Message);

            emailSvc.Verify(es => es.SendEmailAsync("user@mail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            var tokens = await db.VerificationTokens.Include(t => t.VerificationTokenType).ToListAsync();
            Assert.Single(tokens);
            Assert.Equal("EmailVerificationToken", tokens[0].VerificationTokenType.Name);
            Assert.NotEqual(new string('f', 64), tokens[0].Token);
            Assert.True(tokens[0].ExpirationDate > DateTime.UtcNow.AddDays(vt.Value.EmailExpiresInDays - 1));
        }

        [Fact]
        public async Task Register_DuplicateEmail_Confirmed_ThrowsAlreadyRegistered_And_NoResend()
        {
            var db = InMemoryDb();
            db.Users.Add(new User
            {
                Email = "enc",
                EmailHash = "h123",
                PasswordHash = "x",
                FullName = "X",
                IsEmailConfirmed = true,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var (svc, emailIndex, pwd, emailSvc, pii, _, _, _, _) = BuildService(db);
            emailIndex.Setup(x => x.Normalize("user@mail.com")).Returns("user@mail.com");
            emailIndex.Setup(x => x.ComputeHash("user@mail.com")).Returns("h123");
            pwd.Setup(x => x.Hash("Aa1!aaaa")).Returns("hash");
            pii.Setup(x => x.Encrypt("user@mail.com")).Returns("ENC(user@mail.com)");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.RegisterAsync(new RegisterRequest("Jane", "user@mail.com", "Aa1!aaaa")));

            Assert.Equal("Email is already registered.", ex.Message);
            emailSvc.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(db.VerificationTokens);
        }
        #endregion

        #region VerifyEmail
        [Fact]
        public async Task VerifyEmail_TokenNotFound_Fails()
        {
            var db = InMemoryDb();
            var (svc, _, _, _, _, _, _, _, _) = BuildService(db);

            db.VerificationTokenTypes.Add(new VerificationTokenType { Name = "EmailVerificationToken" });
            await db.SaveChangesAsync();

            var resp = await svc.VerifyEmailAsync(new string('a', 64));
            Assert.False(resp.Success);
            Assert.Equal("Token not found.", resp.Message);
            Assert.Contains("emailVerified=false", resp.RedirectUrl);
        }

        [Fact]
        public async Task VerifyEmail_Success_ConfirmsEmail_And_RemovesToken()
        {
            var db = InMemoryDb();
            var (svc, _, _, _, _, _, _, _, _) = BuildService(db);

            var vtt = new VerificationTokenType { Name = "EmailVerificationToken" };
            var user = new User
            {
                FullName = "Jane",
                Email = "enc",
                EmailHash = "h1",
                PasswordHash = "p",
                IsEmailConfirmed = false,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.VerificationTokenTypes.Add(vtt);
            db.VerificationTokens.Add(new VerificationToken
            {
                Token = new string('b', 64),
                VerificationTokenType = vtt,
                ExpirationDate = DateTime.UtcNow.AddMinutes(10),
                UserId = user.Id
            });
            await db.SaveChangesAsync();

            var resp = await svc.VerifyEmailAsync(new string('b', 64));

            Assert.True(resp.Success);
            Assert.Equal("Email verified.", resp.Message);
            Assert.Contains("emailVerified=true", resp.RedirectUrl);

            var updatedUser = await db.Users.FindAsync(user.Id);
            Assert.True(updatedUser!.IsEmailConfirmed);

            Assert.Empty(db.VerificationTokens);
        }

        [Fact]
        public async Task VerifyEmail_InvalidTokenFormat_ThrowsArgument()
        {
            var db = InMemoryDb();
            var (svc, _, _, _, _, _, _, _, _) = BuildService(db);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc.VerifyEmailAsync("not-hex!"));
            Assert.StartsWith("Token format is invalid (must be hex).", ex.Message);
        }
        #endregion

        #region Login
        [Fact]
        public async Task Login_InvalidEmail_ThrowsInvalidOperation()
        {
            var db = InMemoryDb();
            var (svc, emailIndex, _, _, _, _, _, _, _) = BuildService(db);

            emailIndex.Setup(x => x.Normalize("no@user.com")).Returns("no@user.com");
            emailIndex.Setup(x => x.ComputeHash("no@user.com")).Returns("h-no");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.LoginAsync(new LoginRequest("no@user.com", "Whatever1!")));

            Assert.Equal("Invalid credentials.", ex.Message);
        }

        [Fact]
        public async Task Login_WrongPassword_ThrowsInvalidOperation()
        {
            var db = InMemoryDb();

            var user = new User
            {
                Email = "ENC(user@mail.com)",
                EmailHash = "h-user",
                PasswordHash = "stored-hash",
                FullName = "Jane",
                IsEmailConfirmed = true,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var (svc, emailIndex, pwd, _, _, _, _, _, _) = BuildService(db);
            emailIndex.Setup(x => x.Normalize("User@Mail.com")).Returns("user@mail.com");
            emailIndex.Setup(x => x.ComputeHash("user@mail.com")).Returns("h-user");
            pwd.Setup(x => x.Verify("Wrong1!", "stored-hash")).Returns(false);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.LoginAsync(new LoginRequest("User@Mail.com", "Wrong1!")));

            Assert.Equal("Invalid credentials.", ex.Message);
        }

        [Fact]
        public async Task Login_UnconfirmedEmail_ResendsAndThrowsPleaseVerifyWithHint()
        {
            var db = InMemoryDb();

            db.Users.Add(new User
            {
                Email = "ENC(user@mail.com)",
                EmailHash = "h-user",
                PasswordHash = "stored-hash",
                FullName = "Jane",
                IsEmailConfirmed = false,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var (svc, emailIndex, pwd, emailSvc, _, _, _, vt, _) = BuildService(db);
            emailIndex.Setup(x => x.Normalize("user@mail.com")).Returns("user@mail.com");
            emailIndex.Setup(x => x.ComputeHash("user@mail.com")).Returns("h-user");
            pwd.Setup(x => x.Verify("Correct1!", "stored-hash")).Returns(true);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.LoginAsync(new LoginRequest("user@mail.com", "Correct1!")));

            Assert.Equal("Please verify your email. We've resent the verification link.", ex.Message);

            emailSvc.Verify(es => es.SendEmailAsync("user@mail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            var tok = await db.VerificationTokens.Include(t => t.VerificationTokenType).SingleAsync();
            Assert.Equal("EmailVerificationToken", tok.VerificationTokenType.Name);
            Assert.True(tok.ExpirationDate > DateTime.UtcNow.AddDays(vt.Value.EmailExpiresInDays - 1));
        }

        [Fact]
        public async Task Login_UnconfirmedEmail_ReusesExistingUnexpiredToken()
        {
            var db = InMemoryDb();

            var user = new User
            {
                Email = "ENC(user@mail.com)",
                EmailHash = "h-user",
                PasswordHash = "stored-hash",
                FullName = "Jane",
                IsEmailConfirmed = false,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var vtt = new VerificationTokenType { Name = "EmailVerificationToken" };
            db.VerificationTokenTypes.Add(vtt);
            var existingToken = new VerificationToken
            {
                Token = new string('c', 64),
                VerificationTokenType = vtt,
                ExpirationDate = DateTime.UtcNow.AddDays(3),
                UserId = user.Id
            };
            db.VerificationTokens.Add(existingToken);
            await db.SaveChangesAsync();

            var (svc, emailIndex, pwd, emailSvc, _, _, _, _, _) = BuildService(db);
            emailIndex.Setup(x => x.Normalize("user@mail.com")).Returns("user@mail.com");
            emailIndex.Setup(x => x.ComputeHash("user@mail.com")).Returns("h-user");
            pwd.Setup(x => x.Verify("Correct1!", "stored-hash")).Returns(true);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.LoginAsync(new LoginRequest("user@mail.com", "Correct1!")));

            Assert.Equal("Please verify your email. We've resent the verification link.", ex.Message);

            emailSvc.Verify(es => es.SendEmailAsync("user@mail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            Assert.Single(db.VerificationTokens);
            var tok = await db.VerificationTokens.Include(t => t.VerificationTokenType).SingleAsync();
            Assert.Equal(new string('c', 64), tok.Token);
        }


        [Fact]
        public async Task Login_Success_ReturnsTokens()
        {
            var db = InMemoryDb();

            db.Users.Add(new User
            {
                Id = 3,
                Email = "ENC(user@mail.com)",
                EmailHash = "h-user",
                PasswordHash = "stored-hash",
                FullName = "Jane",
                IsEmailConfirmed = true,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var urls = new AppUrls
            {
                ApiBase = "https://localhost:7049",
                VerifyEmailPath = "/auth/verify-email",
                FrontendBase = "https://lateral-inspire.vercel.app/",
                FrontendLoginPath = "/login"
            };

            var (svc, emailIndex, pwd, _, _, tokenSvc, _, _, _) = BuildService(db, urlsOverride: urls);

            emailIndex.Setup(x => x.Normalize("user@mail.com")).Returns("user@mail.com");
            emailIndex.Setup(x => x.ComputeHash("user@mail.com")).Returns("h-user");
            pwd.Setup(x => x.Verify("Correct1!", "stored-hash")).Returns(true);

            var tokens = new TokenResponse(
                AccessToken: "access.jwt",
                RefreshToken: "refresh.jwt",
                AccessExpiresAtUtc: DateTime.UtcNow.AddMinutes(15),
                RefreshExpiresAtUtc: DateTime.UtcNow.AddMinutes(120)
            );
            tokenSvc.Setup(x => x.IssueTokens(3, "user@mail.com")).Returns(tokens);

            var res = await svc.LoginAsync(new LoginRequest("user@mail.com", "Correct1!"));

            Assert.Equal(3, res.UserId);
            Assert.Equal("user@mail.com", res.Email);
            Assert.Equal("Jane", res.FullName);
            Assert.Equal("access.jwt", res.Tokens.AccessToken);
            Assert.Equal("refresh.jwt", res.Tokens.RefreshToken);
            tokenSvc.Verify(x => x.IssueTokens(3, "user@mail.com"), Times.Once);
        }
        #endregion

        #region Refresh
        [Fact]
        public async Task Refresh_InvalidToken_Throws()
        {
            var db = InMemoryDb();
            var (svc, _, _, _, _, tokenSvc, _, _,_) = BuildService(db);

            tokenSvc.Setup(x => x.ValidateRefreshToken("bad")).Returns((ClaimsPrincipal?)null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.RefreshAsync(new RefreshRequest("bad")));

            Assert.Equal("Invalid or expired refresh token.", ex.Message);
        }

        [Fact]
        public async Task Refresh_MissingClaims_Throws()
        {
            var db = InMemoryDb();
            var (svc, _, _, _, _, tokenSvc, _, _, _) = BuildService(db);

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, "user@mail.com") }, "test");
            var principal = new ClaimsPrincipal(identity);
            tokenSvc.Setup(x => x.ValidateRefreshToken("rt")).Returns(principal);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.RefreshAsync(new RefreshRequest("rt")));

            Assert.Equal("Refresh token is missing required claims.", ex.Message);
        }

        [Fact]
        public async Task Refresh_UserNotFound_Throws()
        {
            var db = InMemoryDb();
            var (svc, emailIndex, _, _, _, tokenSvc, _, _, _) = BuildService(db);

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "999"),
                new Claim(ClaimTypes.Email, "user@mail.com")
            }, "test");
            tokenSvc.Setup(x => x.ValidateRefreshToken("rt")).Returns(new ClaimsPrincipal(identity));
            emailIndex.Setup(x => x.Normalize("user@mail.com")).Returns("user@mail.com");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.RefreshAsync(new RefreshRequest("rt")));

            Assert.Equal("User not found.", ex.Message);
        }

        [Fact]
        public async Task Refresh_UnconfirmedEmail_Throws()
        {
            var db = InMemoryDb();
            var (svc, emailIndex, _, _, _, tokenSvc, _, _, _) = BuildService(db);

            var user = new User
            {
                FullName = "Jane",
                Email = "enc",
                EmailHash = "h",
                PasswordHash = "p",
                IsEmailConfirmed = false,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, "User@Mail.com")
            }, "test");
            tokenSvc.Setup(x => x.ValidateRefreshToken("rt")).Returns(new ClaimsPrincipal(identity));
            emailIndex.Setup(x => x.Normalize("User@Mail.com")).Returns("user@mail.com");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.RefreshAsync(new RefreshRequest("rt")));

            Assert.Equal("Please verify your email.", ex.Message);
        }

        [Fact]
        public async Task Refresh_Success_IssuesNewTokens()
        {
            var db = InMemoryDb();
            var (svc, emailIndex, _, _, _, tokenSvc, _, _, _) = BuildService(db);

            var user = new User
            {
                FullName = "Jane",
                Email = "enc",
                EmailHash = "h",
                PasswordHash = "p",
                IsEmailConfirmed = true,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, "User@Mail.com")
            }, "test");
            tokenSvc.Setup(x => x.ValidateRefreshToken("rt")).Returns(new ClaimsPrincipal(identity));
            emailIndex.Setup(x => x.Normalize("User@Mail.com")).Returns("user@mail.com");

            var tokens = new TokenResponse("a", "r", DateTime.UtcNow.AddMinutes(1), DateTime.UtcNow.AddMinutes(2));
            tokenSvc.Setup(x => x.IssueTokens(user.Id, "user@mail.com")).Returns(tokens);

            var res = await svc.RefreshAsync(new RefreshRequest("rt"));
            Assert.Equal(tokens.AccessToken, res.AccessToken);
            Assert.Equal(tokens.RefreshToken, res.RefreshToken);
            tokenSvc.Verify(x => x.IssueTokens(user.Id, "user@mail.com"), Times.Once);
        }
        #endregion

        #region ForgotPassword
        [Fact]
        public async Task ForgotPassword_UserExists_SendsResetEmail_And_CreatesToken()
        {
            var db = InMemoryDb();
            var (svc, emailIndex, _, emailSvc, _, _, _, vt, _) = BuildService(db,
                vtOverride: new VerificationTokenSettings { EmailExpiresInDays = 7, PasswordResetExpiresInMinutes = 45 });

            var user = new User
            {
                FullName = "Jane",
                Email = "enc",
                EmailHash = "h-user",
                PasswordHash = "p",
                IsEmailConfirmed = true,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            emailIndex.Setup(x => x.Normalize("User@Mail.com")).Returns("user@mail.com");
            emailIndex.Setup(x => x.ComputeHash("user@mail.com")).Returns("h-user");

            var resp = await svc.ForgotPasswordAsync(new ForgotPasswordRequest("User@Mail.com"));
            Assert.True(resp.Success);

            var tok = await db.VerificationTokens.Include(t => t.VerificationTokenType).SingleAsync();
            Assert.Equal("PasswordResetToken", tok.VerificationTokenType.Name);
            Assert.Equal(user.Id, tok.UserId);
            Assert.True(tok.ExpirationDate > DateTime.UtcNow.AddMinutes(vt.Value.PasswordResetExpiresInMinutes - 5));
            emailSvc.Verify(es => es.SendEmailAsync("user@mail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ForgotPassword_UserNotFound_StillSuccess_NoEmail_NoToken()
        {
            var db = InMemoryDb();
            var (svc, emailIndex, _, emailSvc, _, _, _, _, _) = BuildService(db);

            emailIndex.Setup(x => x.Normalize("no@user.com")).Returns("no@user.com");
            emailIndex.Setup(x => x.ComputeHash("no@user.com")).Returns("h-no");

            var resp = await svc.ForgotPasswordAsync(new ForgotPasswordRequest("no@user.com"));
            Assert.True(resp.Success);
            Assert.Empty(db.VerificationTokens);
            emailSvc.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        #endregion

        #region ResetPassword
        [Fact]
        public async Task ResetPassword_SystemNotInitialized_Throws()
        {
            var db = InMemoryDb();
            var (svc, _, _, _, _, _, _, _, _) = BuildService(db);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ResetPasswordAsync(new ResetPasswordRequest("deadbeef", "Aa1!aaaa")));

            Assert.Equal("Password reset system not initialized.", ex.Message);
        }

        [Fact]
        public async Task ResetPassword_InvalidToken_Throws()
        {
            var db = InMemoryDb();
            var (svc, _, _, _, _, _, _, _, _) = BuildService(db);

            db.VerificationTokenTypes.Add(new VerificationTokenType { Name = "PasswordResetToken" });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ResetPasswordAsync(new ResetPasswordRequest("not-found", "Aa1!aaaa")));

            Assert.Equal("Invalid token.", ex.Message);
        }

        [Fact]
        public async Task ResetPassword_Expired_Throws()
        {
            var db = InMemoryDb();
            var (svc, _, _, _, _, _, _, _, _) = BuildService(db);

            var vtt = new VerificationTokenType { Name = "PasswordResetToken" };
            var user = new User
            {
                FullName = "Jane",
                Email = "enc",
                EmailHash = "h",
                PasswordHash = "old",
                IsEmailConfirmed = true,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            db.VerificationTokenTypes.Add(vtt);
            db.VerificationTokens.Add(new VerificationToken
            {
                Token = "tok",
                VerificationTokenType = vtt,
                UserId = user.Id,
                ExpirationDate = DateTime.UtcNow.AddMinutes(-1)
            });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ResetPasswordAsync(new ResetPasswordRequest("tok", "Aa1!aaaa")));

            Assert.Equal("Token expired.", ex.Message);
        }

        [Fact]
        public async Task ResetPassword_Success_UpdatesHash_And_RemovesToken()
        {
            var db = InMemoryDb();
            var (svc, _, pwd, _, _, _, _, _, _) = BuildService(db);

            var vtt = new VerificationTokenType { Name = "PasswordResetToken" };
            var user = new User
            {
                FullName = "Jane",
                Email = "enc",
                EmailHash = "h",
                PasswordHash = "old",
                IsEmailConfirmed = true,
                NotificationPreferenceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            db.VerificationTokenTypes.Add(vtt);
            db.VerificationTokens.Add(new VerificationToken
            {
                Token = "tok",
                VerificationTokenType = vtt,
                UserId = user.Id,
                ExpirationDate = DateTime.UtcNow.AddMinutes(10)
            });
            await db.SaveChangesAsync();

            pwd.Setup(x => x.Hash("NewP@ssw0rd")).Returns("new-hash");

            var resp = await svc.ResetPasswordAsync(new ResetPasswordRequest("tok", "NewP@ssw0rd"));
            Assert.True(resp.Success);

            var updatedUser = await db.Users.FindAsync(user.Id);
            Assert.Equal("new-hash", updatedUser!.PasswordHash);
            Assert.Empty(db.VerificationTokens);
        }
        #endregion

        #region Input Validation
        [Fact]
        public async Task Login_InvalidInput_ThrowsArgument()
        {
            var db = InMemoryDb();
            var (svc, _, _, _, _, _, _, _, _) = BuildService(db);
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc.LoginAsync(new LoginRequest("", "")));
            Assert.Contains("Email is required.", ex.Message);
        }

        [Fact]
        public async Task Refresh_BlankToken_ThrowsArgument()
        {
            var db = InMemoryDb();
            var (svc, _, _, _, _, _, _, _, _) = BuildService(db);
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc.RefreshAsync(new RefreshRequest("")));
            Assert.Contains("Refresh token is required.", ex.Message);
        }

        [Fact]
        public async Task ForgotPassword_InvalidEmail_ThrowsArgument()
        {
            var db = InMemoryDb();
            var (svc, _, _, _, _, _, _, _, _) = BuildService(db);
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc.ForgotPasswordAsync(new ForgotPasswordRequest("not-an-email")));
            Assert.Contains("Email format is invalid.", ex.Message);
        }

        [Fact]
        public async Task ResetPassword_BlankFields_ThrowArgument()
        {
            var db = InMemoryDb();
            var (svc, _, _, _, _, _, _, _, _) = BuildService(db);
            var ex1 = await Assert.ThrowsAsync<ArgumentException>(() => svc.ResetPasswordAsync(new ResetPasswordRequest("", "newpass")));
            Assert.Contains("Token is required.", ex1.Message);

            var ex2 = await Assert.ThrowsAsync<ArgumentException>(() => svc.ResetPasswordAsync(new ResetPasswordRequest("abc", "")));
            Assert.Contains("New password is required.", ex2.Message);
        }
        #endregion

        #region DeleteExpiredVerificationTokens
        [Fact]
        public async Task DeleteExpiredVerificationTokens_Deletes_OnlyExpired_AndHonorsType()
        {
            using var ctx = SqliteInMemoryDb();

            var pref = new NotificationPreference { Name = "All" };
            ctx.NotificationPreferences.Add(pref);
            await ctx.SaveChangesAsync();

            var emailType = new VerificationTokenType { Id = (int)VerificationTokenEnum.EmailVerificationToken, Name = "EmailVerificationToken" };
            var resetType = new VerificationTokenType { Id = (int)VerificationTokenEnum.ForgotPasswordToken, Name = "ForgotPasswordToken" };
            ctx.VerificationTokenTypes.AddRange(emailType, resetType);

            var user = new User
            {
                FullName = "T",
                Email = "x",
                EmailHash = "h",
                PasswordHash = "p",
                IsEmailConfirmed = true,
                NotificationPreferenceId = pref.Id, 
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();

            var now = DateTime.UtcNow;
            var t1 = new VerificationToken { Token = "e1", VerificationTokenTypeId = emailType.Id, ExpirationDate = now.AddMinutes(-10), UserId = user.Id };
            var t2 = new VerificationToken { Token = "e2", VerificationTokenTypeId = emailType.Id, ExpirationDate = now.AddMinutes(10), UserId = user.Id };
            var t3 = new VerificationToken { Token = "r1", VerificationTokenTypeId = resetType.Id, ExpirationDate = now.AddMinutes(-5), UserId = user.Id };
            ctx.VerificationTokens.AddRange(t1, t2, t3);
            await ctx.SaveChangesAsync();

            var (svc, _, _, _, _, _, _, _, _) = BuildService(ctx);

            var deleted = await svc.DeleteExpiredVerificationTokensAsync(systemRun: true, type: VerificationTokenEnum.EmailVerificationToken);

            Assert.Equal(1, deleted);
            var remaining = await ctx.VerificationTokens.AsNoTracking().ToListAsync();
            Assert.Contains(remaining, v => v.Token == "e2");
            Assert.Contains(remaining, v => v.Token == "r1");
            Assert.DoesNotContain(remaining, v => v.Token == "e1");
        }

        [Fact]
        public async Task DeleteExpiredVerificationTokens_Schedules_WhenSystemRunFalse()
        {
            using var ctx = InMemoryDb(); 

            var built = BuildService(ctx);
            var svc = built.svc;
            var jobs = built.jobs;

            DateTimeOffset? scheduledFor = null;

            jobs.Setup(j => j.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()))
                .Callback<Hangfire.Common.Job, Hangfire.States.IState>((job, state) =>
                {
                    Assert.Equal(typeof(IAuthService), job.Type);
                    Assert.Equal(nameof(IAuthService.DeleteExpiredVerificationTokensAsync), job.Method.Name);

                    var enqueueAtProp = state.GetType().GetProperty("EnqueueAt");
                    Assert.NotNull(enqueueAtProp);

                    var raw = enqueueAtProp!.GetValue(state);
                    if (raw is DateTimeOffset dto)
                    {
                        scheduledFor = dto.ToUniversalTime();
                    }
                    else if (raw is DateTime dt)
                    {
                        var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                        scheduledFor = new DateTimeOffset(utc);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unexpected EnqueueAt type: {raw?.GetType().FullName ?? "null"}");
                    }
                })
                .Returns("job-id-1");

            var deleted = await svc.DeleteExpiredVerificationTokensAsync(systemRun: false, type: VerificationTokenEnum.EmailVerificationToken);

            Assert.Equal(0, deleted);
            jobs.Verify(j => j.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()), Times.Once);
            Assert.True(scheduledFor.HasValue);

            var now = DateTime.UtcNow;
            var expected = new DateTime(now.Year, now.Month, now.Day, 2, 0, 0, DateTimeKind.Utc);
            if (now >= expected) expected = expected.AddDays(1);

            Assert.InRange(scheduledFor!.Value.UtcDateTime, expected.AddMinutes(-1), expected.AddMinutes(1));
        }
        #endregion
    }
}
