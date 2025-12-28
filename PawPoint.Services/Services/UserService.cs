using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;
using Microsoft.EntityFrameworkCore;
using ThreadingTask = System.Threading.Tasks.Task;

namespace PawPoint.Services.Services
{
    public sealed class UserService(Context db,
        IProfilePictureService pictureService,
        IProfilePictureUrlFactory urlFactory,
        IPiiEncryptionService pii,
        IPasswordService? passwordService = null) : IUserService
    {
        private readonly Context _db = db;
        private readonly IProfilePictureService _pictureService = pictureService;
        private readonly IProfilePictureUrlFactory _urlFactory = urlFactory;
        private readonly IPiiEncryptionService _pii = pii;
        private readonly IPasswordService? _passwords = passwordService;
        public async Task<UserProfileResponse> GetProfileAsync(int userId)
        {
            userId = ValidateUserId(userId);

            var user = await _db.Users
                .Include(u => u.NotificationPreference)
                .FirstOrDefaultAsync(u => u.Id == userId);

            ValidateUserFound(user, userId);

            return new UserProfileResponse(
                Email: DecryptOrRaw(user!.Email),
                FullName: user.FullName,
                ProfilePictureUrl: _urlFactory.BuildPublicUrl(user.ProfilePictureUrl),
                PhoneNumber: user.PhoneNumber,
                NotificationPreference: user.NotificationPreference.Name
            );
        }

        public async Task<UserProfileResponse> UpdateProfileAsync(int userId, UpdateUserProfileRequest request)
        {
            userId = ValidateUserId(userId);
            request = ValidateRequest(request);

            var user = await _db.Users
                .Include(u => u.NotificationPreference)
                .FirstOrDefaultAsync(u => u.Id == userId);

            ValidateUserFound(user, userId);

            if (request.FullName is not null)
            {
                user!.FullName = ValidateFullName(request.FullName);
            }

            if (request.PhoneNumber is not null)
            {
                user!.PhoneNumber = NormalizePhone(request.PhoneNumber);
            }

            if (request.ProfilePicture is not null)
            {
                if (!string.IsNullOrEmpty(user!.ProfilePictureUrl))
                {
                    await _pictureService.DeleteAsync(user.ProfilePictureUrl);
                }

                var relativePath = await _pictureService.UploadAsync(userId, request.ProfilePicture);
                user.ProfilePictureUrl = relativePath;
            }

            await _db.SaveChangesAsync();

            return new UserProfileResponse(
                Email: DecryptOrRaw(user!.Email),
                FullName: user.FullName,
                ProfilePictureUrl: _urlFactory.BuildPublicUrl(user.ProfilePictureUrl),
                PhoneNumber: user.PhoneNumber,
                NotificationPreference: user.NotificationPreference.Name
            );
        }

        public async ThreadingTask ChangePasswordAsync(int userId, ChangePasswordRequest request)
        {
            userId = ValidateUserId(userId);
            request = ValidateChangePasswordRequest(request);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            ValidateUserFound(user, userId);

            if (_passwords is null)
            {
                throw new InvalidOperationException("Password service is not configured.");
            }

            var ok = _passwords.Verify(request.CurrentPassword, user!.PasswordHash);
            _ = ok switch
            {
                false => throw new InvalidOperationException("Current password is incorrect."),
                true => true
            };

            if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
            {
                throw new ArgumentException("New password must be different from the current password.", nameof(request.NewPassword));
            }

            var newHash = _passwords.Hash(request.NewPassword);
            user.PasswordHash = newHash;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public async Task<UserSettingsResponse> GetSettingsAsync(int userId)
        {
            userId = ValidateUserId(userId);

            var user = await _db.Users
                .Include(u => u.NotificationPreference)
                .FirstOrDefaultAsync(u => u.Id == userId);

            ValidateUserFound(user, userId);

            return new UserSettingsResponse(
                NotificationPreferenceId: user!.NotificationPreferenceId,
                NotificationPreference: user.NotificationPreference.Name
            );
        }

        public async Task<UserSettingsResponse> UpdateSettingsAsync(int userId, UpdateUserSettingsRequest request)
        {
            if (userId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(userId), "User id must be a positive integer.");
            }

            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.NotificationPreferenceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.NotificationPreferenceId),
                    "NotificationPreferenceId must be a positive integer.");
            }

            var user = await _db.Users
                .Include(u => u.NotificationPreference)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
            {
                throw new KeyNotFoundException($"User with id {userId} was not found.");
            }

            var pref = await _db.NotificationPreferences
                .FirstOrDefaultAsync(p => p.Id == request.NotificationPreferenceId);

            if (pref is null)
            {
                throw new KeyNotFoundException(
                    $"Notification preference with id {request.NotificationPreferenceId} was not found.");
            }

            if (user.NotificationPreferenceId == pref.Id)
            {
                return new UserSettingsResponse(pref.Id, pref.Name);
            }

            user.NotificationPreferenceId = pref.Id;
            user.NotificationPreference = pref;

            await _db.SaveChangesAsync();

            return new UserSettingsResponse(pref.Id, pref.Name);
        }

        public async ThreadingTask SoftDeleteUserAsync(int userId)
        {
            if (userId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(userId), "User id must be a positive integer.");
            }

            await _db.Users
                .IgnoreQueryFilters()
                .Where(u => u.Id == userId && !u.IsDeleted)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(x => x.IsDeleted, true)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
        }

        #region Private Methods
        private static int ValidateUserId(int userId)
        {
            _ = userId switch
            {
                <= 0 => throw new ArgumentOutOfRangeException(nameof(userId), "User id must be a positive integer."),
                _ => true
            };
            return userId;
        }

        private static void ValidateUserFound(User? user, int userId)
        {
            _ = (user is null) switch
            {
                true => throw new KeyNotFoundException($"User with id {userId} was not found."),
                false => true
            };
        }
        private string DecryptOrRaw(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            try
            {
                return _pii.Decrypt(value);
            }
            catch
            {
                return value;
            }
        }
        private static UpdateUserProfileRequest ValidateRequest(UpdateUserProfileRequest req)
        {
            if (req is null)
            {
                throw new ArgumentNullException(nameof(req));
            }

            return req;
        }

        private static string ValidateFullName(string fullName)
        {
            _ = string.IsNullOrWhiteSpace(fullName) switch
            {
                true => throw new ArgumentException("Full name is required.", nameof(fullName)),
                false => true
            };

            var name = fullName.Trim();

            _ = name.Length switch
            {
                > 100 => throw new ArgumentOutOfRangeException(nameof(fullName), "Full name must be <= 100 characters."),
                _ => true
            };

            return name;
        }

        private static string? NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return null;
            }

            var trimmed = phone.Trim();
            if (trimmed.Length > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(phone), "Phone number too long.");
            }

            return trimmed;
        }

        private static ChangePasswordRequest ValidateChangePasswordRequest(ChangePasswordRequest req)
        {
            var curBlank = string.IsNullOrWhiteSpace(req?.CurrentPassword);
            var newBlank = string.IsNullOrWhiteSpace(req?.NewPassword);
            _ = (req, curBlank, newBlank) switch
            {
                (null, _, _) => throw new ArgumentException("Request cannot be null.", nameof(req)),
                (_, true, _) => throw new ArgumentException("Current password is required.", nameof(req.CurrentPassword)),
                (_, _, true) => throw new ArgumentException("New password is required.", nameof(req.NewPassword)),
                _ => true
            };
            return req!;
        }
        #endregion
    }
}
