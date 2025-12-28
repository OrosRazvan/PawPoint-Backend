using Hangfire;
using PawPoint.Common.Helpers;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.DB.Enums;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;

namespace PawPoint.Services.Services
{
    public sealed class AuthService(
        Context db,
        IEmailIndexService emailIndex,
        IPasswordService passwordService,
        IEmailService emailService,
        IPiiEncryptionService pii,
        IOptions<AppUrls> urlOptions,
        IOptions<VerificationTokenSettings> vtOptions,
        ITokenService tokenService,
        ITemplateRenderer templateRenderer,
        IOptions<TemplateSettings> templateOptions,
        IBackgroundJobClient jobs
    ) : IAuthService
    {
        private readonly AppUrls _urls = urlOptions.Value;
        private readonly VerificationTokenSettings _vt = vtOptions.Value;
        private readonly ITemplateRenderer _templates = templateRenderer;
        private readonly TemplateSettings _tpl = templateOptions.Value;

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            ValidateRegisterInput(request);

            var normalizedEmail = emailIndex.Normalize(request.Email);
            var emailHash = emailIndex.ComputeHash(normalizedEmail);

            var existing = await db.Users.FirstOrDefaultAsync(u => u.EmailHash == emailHash);
            if (existing is not null)
            {
                if (existing.IsEmailConfirmed)
                {
                    throw new InvalidOperationException("Email is already registered.");
                }
                else
                {
                    var tokenValue = await EnsureEmailVerificationTokenAsync(existing.Id);
                    await SendVerificationEmailAsync(
                        toEmail: normalizedEmail,
                        fullName: string.IsNullOrWhiteSpace(existing.FullName) ? request.FullName : existing.FullName,
                        tokenValue: tokenValue
                    );

                    throw new InvalidOperationException("Account exists but email is unverified. We resent the verification email.");
                }
            }

            var pwdHash = passwordService.Hash(request.Password);
            var defaultNotifPrefId = await GetOrCreateDefaultNotificationPreferenceIdAsync();
            var encryptedEmail = pii.Encrypt(normalizedEmail);

            var user = new User
            {
                Email = encryptedEmail,
                EmailHash = emailHash,
                PasswordHash = pwdHash,
                FullName = request.FullName,
                IsEmailConfirmed = false,
                NotificationPreferenceId = defaultNotifPrefId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var firstToken = await EnsureEmailVerificationTokenAsync(user.Id);
            await SendVerificationEmailAsync(
                toEmail: normalizedEmail,
                fullName: request.FullName,
                tokenValue: firstToken
            );

            return new RegisterResponse(
                UserId: user.Id,
                Email: normalizedEmail,
                FullName: request.FullName,
                EmailConfirmed: user.IsEmailConfirmed
            );
        }

        public async Task<VerifyEmailResponse> VerifyEmailAsync(string token)
        {
            ValidateVerifyEmailInput(token);

            var verifyType = await db.VerificationTokenTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name == "EmailVerificationToken");
            if (verifyType is null)
            {
                return Fail("Verification system not initialized.");
            }
                
            var vt = await db.VerificationTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Token == token && x.VerificationTokenTypeId == verifyType.Id);

            if (vt is null) 
            {
                return Fail("Token not found."); 
            }

            if (vt.ExpirationDate < DateTime.UtcNow)
            {
                return Fail("Token expired.");
            }

            if (vt.User is null)
            {
                return Fail("User not found.");
            }

            vt.User.IsEmailConfirmed = true;
            vt.User.UpdatedAt = DateTime.UtcNow;
            db.VerificationTokens.Remove(vt);
            await db.SaveChangesAsync();

            var redirect = BuildFrontendLoginRedirectUrl(emailVerified: true);
            return new VerifyEmailResponse(true, redirect, "Email verified.");
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            ValidateLoginInput(request);

            var normalizedEmail = emailIndex.Normalize(request.Email);
            var emailHash = emailIndex.ComputeHash(normalizedEmail);

            var user = await db.Users.FirstOrDefaultAsync(u => u.EmailHash == emailHash);
            if (user is null)
            {
                throw new InvalidOperationException("Invalid credentials.");
            }

            if (!passwordService.Verify(request.Password, user.PasswordHash))
            {
                throw new InvalidOperationException("Invalid credentials.");
            }

            if (!user.IsEmailConfirmed)
            {
                var tokenValue = await EnsureEmailVerificationTokenAsync(user.Id);
                await SendVerificationEmailAsync(
                    toEmail: normalizedEmail,
                    fullName: string.IsNullOrWhiteSpace(user.FullName) ? "there" : user.FullName,
                    tokenValue: tokenValue
                );

                throw new InvalidOperationException("Please verify your email. We've resent the verification link.");
            }

            var tokens = tokenService.IssueTokens(user.Id, normalizedEmail);

            return new LoginResponse(
                UserId: user.Id,
                Email: normalizedEmail,
                FullName: user.FullName,
                Tokens: tokens
            );
        }

        public async Task<TokenResponse> RefreshAsync(RefreshRequest request)
        {
            ValidateRefreshInput(request);

            var principal = tokenService.ValidateRefreshToken(request.RefreshToken);
            if (principal is null) 
            { 
                throw new InvalidOperationException("Invalid or expired refresh token.");
            }

            var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var emailFromToken = principal.FindFirst(ClaimTypes.Email)?.Value;

            if (!int.TryParse(userIdStr, out var userId) || string.IsNullOrWhiteSpace(emailFromToken)) 
            {
                throw new InvalidOperationException("Refresh token is missing required claims.");
            }

            var user = await db.Users.FindAsync(userId);
            if (user is null)
            {
                throw new InvalidOperationException("User not found.");
            }
            if (!user.IsEmailConfirmed)
            {
                throw new InvalidOperationException("Please verify your email.");
            }

            var normalizedEmail = emailIndex.Normalize(emailFromToken);
            return tokenService.IssueTokens(user.Id, normalizedEmail);
        }

        public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            ValidateForgotPasswordInput(request);

            var normalizedEmail = emailIndex.Normalize(request.Email);
            var emailHash = emailIndex.ComputeHash(normalizedEmail);

            var user = await db.Users.FirstOrDefaultAsync(u => u.EmailHash == emailHash);

            if (user is not null)
            {
                var resetTypeId = await EnsureVerificationTypeAsync("PasswordResetToken");
                var tokenValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
                var expires = DateTime.UtcNow.AddMinutes(_vt.PasswordResetExpiresInMinutes);

                db.VerificationTokens.Add(new VerificationToken
                {
                    Token = tokenValue,
                    VerificationTokenTypeId = resetTypeId,
                    ExpirationDate = expires,
                    UserId = user.Id
                });
                await db.SaveChangesAsync();

                var resetLink = $"{_urls.FrontendBase.TrimEnd('/')}/reset-password?token={tokenValue}";

                var model = new Dictionary<string, string>
                {
                    ["FullName"] = string.IsNullOrWhiteSpace(user.FullName) ? "there" : user.FullName,
                    ["ResetLink"] = resetLink,
                    ["Token"] = tokenValue 
                };

                var html = await _templates.RenderAsync(_tpl.Email.ResetHtml, model);
                var text = await _templates.RenderAsync(_tpl.Email.ResetText, model);

                await emailService.SendEmailAsync(
                    toEmail: normalizedEmail,
                    title: "Reset your Inspire password",
                    textBody: text,
                    htmlBody: html
                );
            }

            return new ForgotPasswordResponse(true);
        }

        public async Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request)
        {
            ValidateResetPasswordInput(request);

            var resetType = await db.VerificationTokenTypes
                .FirstOrDefaultAsync(t => t.Name == "PasswordResetToken");

            if (resetType is null)
            {
                throw new InvalidOperationException("Password reset system not initialized.");
            }

            var vt = await db.VerificationTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Token == request.Token && x.VerificationTokenTypeId == resetType.Id);

            if (vt is null)
            {
                throw new InvalidOperationException("Invalid token.");
            }
            if (vt.ExpirationDate < DateTime.UtcNow)
            {
                throw new InvalidOperationException("Token expired.");
            }
            if (vt.User is null)
            {
                throw new InvalidOperationException("User not found.");
            }

            var newHash = passwordService.Hash(request.NewPassword);
            vt.User.PasswordHash = newHash;
            vt.User.UpdatedAt = DateTime.UtcNow;

            db.VerificationTokens.Remove(vt);
            await db.SaveChangesAsync();

            return new ResetPasswordResponse(true);
        }

        public async Task<int> DeleteExpiredVerificationTokensAsync(bool systemRun, VerificationTokenEnum? type = null)
        {
            if (!systemRun)
            {
                var runAt = NextDailyUtc(2);
                jobs.Schedule<IAuthService>(s => s.DeleteExpiredVerificationTokensAsync(true, type), runAt);
                return 0;
            }

            var now = DateTime.UtcNow;
            var q = db.VerificationTokens.Where(v => v.ExpirationDate < now);
            if (type.HasValue)
            {
                q = q.Where(v => v.VerificationTokenTypeId == (int)type.Value);
            }
            return await q.ExecuteDeleteAsync();
        }

        #region Validation
        private static void ValidateRefreshInput(RefreshRequest req)
        {
            var blank = string.IsNullOrWhiteSpace(req?.RefreshToken);
            _ = (req, blank) switch
            {
                (null, _) => throw new ArgumentException("Request cannot be null.", nameof(req)),
                (_, true) => throw new ArgumentException("Refresh token is required.", nameof(req.RefreshToken)),
                _ => true
            };
        }

        private static void ValidateRegisterInput(RegisterRequest req)
        {
            var isFullNameBlank = string.IsNullOrWhiteSpace(req?.FullName);
            var isEmailBlank = string.IsNullOrWhiteSpace(req?.Email);
            var emailLooksValid = !isEmailBlank && System.Net.Mail.MailAddress.TryCreate(req!.Email, out _);
            var isPasswordBlank = string.IsNullOrWhiteSpace(req?.Password);
            var fullNameLen = req?.FullName?.Length ?? 0;

            _ = (req, isFullNameBlank, fullNameLen, isEmailBlank, emailLooksValid, isPasswordBlank) switch
            {
                (null, _, _, _, _, _) => throw new ArgumentException("Request cannot be null.", nameof(req)),
                (_, true, _, _, _, _) => throw new ArgumentException("Full name is required.", nameof(req.FullName)),
                (_, _, > 200, _, _, _) => throw new ArgumentException("Full name too long (max 200).", nameof(req.FullName)),
                (_, _, _, true, _, _) => throw new ArgumentException("Email is required.", nameof(req.Email)),
                (_, _, _, _, false, _) => throw new ArgumentException("Email format is invalid.", nameof(req.Email)),
                (_, _, _, _, _, true) => throw new ArgumentException("Password is required.", nameof(req.Password)),
                _ => true
            };
        }

        private static void ValidateVerifyEmailInput(string token)
        {
            var isBlank = string.IsNullOrWhiteSpace(token);
            var looksHex = !isBlank && token!.All(c =>
                (c >= '0' && c <= '9') ||
                (c >= 'a' && c <= 'f') ||
                (c >= 'A' && c <= 'F'));

            _ = (token, isBlank, looksHex, token?.Length) switch
            {
                (null or "", true, _, _) => throw new ArgumentException("Token is required.", nameof(token)),
                (_, _, false, _) => throw new ArgumentException("Token format is invalid (must be hex).", nameof(token)),
                (_, _, _, < 32 or > 128) => throw new ArgumentException("Token length is invalid.", nameof(token)),
                _ => true
            };
        }

        private static void ValidateLoginInput(LoginRequest req)
        {
            var isEmailBlank = string.IsNullOrWhiteSpace(req?.Email);
            var emailLooksValid = !isEmailBlank && System.Net.Mail.MailAddress.TryCreate(req!.Email, out _);
            var isPasswordBlank = string.IsNullOrWhiteSpace(req?.Password);

            _ = (req, isEmailBlank, emailLooksValid, isPasswordBlank) switch
            {
                (null, _, _, _) => throw new ArgumentException("Request cannot be null.", nameof(req)),
                (_, true, _, _) => throw new ArgumentException("Email is required.", nameof(req.Email)),
                (_, _, false, _) => throw new ArgumentException("Email format is invalid.", nameof(req.Email)),
                (_, _, _, true) => throw new ArgumentException("Password is required.", nameof(req.Password)),
                _ => true
            };
        }
        private static void ValidateForgotPasswordInput(ForgotPasswordRequest req)
        {
            var blank = string.IsNullOrWhiteSpace(req?.Email);
            var emailValid = !blank && System.Net.Mail.MailAddress.TryCreate(req!.Email, out _);

            _ = (req, blank, emailValid) switch
            {
                (null, _, _) => throw new ArgumentException("Request cannot be null.", nameof(req)),
                (_, true, _) => throw new ArgumentException("Email is required.", nameof(req.Email)),
                (_, _, false) => throw new ArgumentException("Email format is invalid.", nameof(req.Email)),
                _ => true
            };
        }

        private static void ValidateResetPasswordInput(ResetPasswordRequest req)
        {
            var tokenBlank = string.IsNullOrWhiteSpace(req?.Token);
            var pwdBlank = string.IsNullOrWhiteSpace(req?.NewPassword);
            var pwdLen = req?.NewPassword?.Length ?? 0;

            _ = (req, tokenBlank, pwdBlank, pwdLen) switch
            {
                (null, _, _, _) => throw new ArgumentException("Request cannot be null.", nameof(req)),
                (_, true, _, _) => throw new ArgumentException("Token is required.", nameof(req.Token)),
                (_, _, true, _) => throw new ArgumentException("New password is required.", nameof(req.NewPassword)),
                (_, _, _, < 8) => throw new ArgumentException("New password must be at least 8 characters.", nameof(req.NewPassword)),
                _ => true
            };
        }
        #endregion

        #region Helpers
        private static DateTime NextDailyUtc(int hour)
        {
            var now = DateTime.UtcNow;
            var at = now.Date.AddHours(hour);
            return now >= at ? at.AddDays(1) : at;
        }
        private async Task<int> GetOrCreateDefaultNotificationPreferenceIdAsync()
        {
            var id = await db.NotificationPreferences
                .Where(p => p.Name == "All")
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            if (id != 0)
            {
                return id;
            }

            var pref = new NotificationPreference { Name = "All" };
            db.NotificationPreferences.Add(pref);
            await db.SaveChangesAsync();
            return pref.Id;
        }

        private async Task<int> EnsureVerificationTypeAsync(string name)
        {
            var vt = await db.VerificationTokenTypes.FirstOrDefaultAsync(x => x.Name == name);
            if (vt is not null)
            {
                return vt.Id;
            }

            vt = new VerificationTokenType { Name = name };
            db.VerificationTokenTypes.Add(vt);
            await db.SaveChangesAsync();
            return vt.Id;
        }

        private string BuildFrontendLoginRedirectUrl(bool emailVerified)
        {
            var flag = emailVerified ? "true" : "false";
            return $"{_urls.FrontendBase.TrimEnd('/')}{_urls.FrontendLoginPath}?emailVerified={flag}";
        }

        private VerifyEmailResponse Fail(string message)
        {
            var fallback = $"{_urls.FrontendBase.TrimEnd('/')}{_urls.FrontendLoginPath}?emailVerified=false&error={Uri.EscapeDataString(message)}";
            return new VerifyEmailResponse(false, fallback, message);
        }
        private async Task<string> EnsureEmailVerificationTokenAsync(int userId)
        {
            var verifyTypeId = await EnsureVerificationTypeAsync("EmailVerificationToken");
            var now = DateTime.UtcNow;

            var reusable = await db.VerificationTokens
                .Where(v => v.UserId == userId && v.VerificationTokenTypeId == verifyTypeId && v.ExpirationDate >= now)
                .OrderByDescending(v => v.ExpirationDate)
                .FirstOrDefaultAsync();

            if (reusable is not null)
            {
                return reusable.Token;
            }

            var expired = await db.VerificationTokens
                .Where(v => v.UserId == userId && v.VerificationTokenTypeId == verifyTypeId && v.ExpirationDate < now)
                .ToListAsync();

            if (expired.Count > 0)
            {
                db.VerificationTokens.RemoveRange(expired);
                await db.SaveChangesAsync();
            }

            var tokenValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

            db.VerificationTokens.Add(new VerificationToken
            {
                Token = tokenValue,
                VerificationTokenTypeId = verifyTypeId,
                ExpirationDate = DateTime.UtcNow.AddDays(_vt.EmailExpiresInDays),
                UserId = userId
            });
            await db.SaveChangesAsync();

            return tokenValue;
        }

        private async System.Threading.Tasks.Task SendVerificationEmailAsync(string toEmail, string fullName, string tokenValue)
        {
            var apiBase = _urls.ApiBase.TrimEnd('/');
            var verifyLink = $"{apiBase}/auth/verify-email-link?token={tokenValue}";

            var model = new Dictionary<string, string>
            {
                ["FullName"] = string.IsNullOrWhiteSpace(fullName) ? "there" : fullName,
                ["VerifyLink"] = verifyLink
            };

            var html = await _templates.RenderAsync(_tpl.Email.VerifyHtml, model);
            var text = await _templates.RenderAsync(_tpl.Email.VerifyText, model);

            await emailService.SendEmailAsync(
                toEmail: toEmail,
                title: "Verify your Inspire account",
                textBody: text,
                htmlBody: html
            );
        }
        #endregion
    }
}
