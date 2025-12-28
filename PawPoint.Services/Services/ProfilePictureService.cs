using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PawPoint.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace PawPoint.Services.Services
{
    public sealed class ProfilePictureService(
        [FromKeyedServices("profile-pics")] BlobContainerClient container) : IProfilePictureService
    {
        private readonly BlobContainerClient _container = container;

        public async Task<string> UploadAsync(int userId, IFormFile file)
        {
            userId = ValidateUserId(userId);
            ValidateFile(file);

            await _container.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var ext = GetExtensionFrom(file.ContentType, file.FileName);
            var blobName = BuildBlobName(userId, ext);

            var blob = _container.GetBlobClient(blobName);

            await using var stream = file.OpenReadStream();
            var headers = new BlobHttpHeaders
            {
                ContentType = file.ContentType,
                CacheControl = "public, max-age=31536000"
            };

            await blob.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers });

            return blobName; 
        }

        public async Task DeleteAsync(string relativePath)
        {
            relativePath = ValidateRelativePath(relativePath);
            var blob = _container.GetBlobClient(relativePath);
            await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
        }

        #region Private Methods
        private static int ValidateUserId(int userId)
        {
            _ = userId switch
            {
                <= 0 => throw new ArgumentOutOfRangeException(nameof(userId), "UserId must be positive."),
                _ => true
            };
            return userId;
        }

        private static void ValidateFile(IFormFile file)
        {
            if (file is null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (file.Length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(file), "File is empty.");
            }

            _ = file.ContentType?.ToLowerInvariant() switch
            {
                "image/jpeg" => true,
                "image/png" => true,
                "image/webp" => true,
                _ => throw new NotSupportedException($"Unsupported content type: {file.ContentType}")
            };

            const long maxBytes = 5 * 1024 * 1024;
            if (file.Length > maxBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(file), $"File too large. Max {maxBytes} bytes.");
            }
        }

        private static string GetExtensionFrom(string? contentType, string fileName)
        {
            var ext = (contentType ?? string.Empty).ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => Path.GetExtension(fileName)
            };

            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".bin";
            }

            return ext;
        }

        private static string BuildBlobName(int userId, string ext)
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
            var guid = Guid.NewGuid().ToString("N");
            return $"users/{userId}/pp-{stamp}-{guid}{ext}";
        }

        private static string ValidateRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Relative path required.", nameof(relativePath));
            }

            if (relativePath.Contains("://", StringComparison.Ordinal))
            {
                throw new ArgumentException("Expected a relative path, not an absolute URL.", nameof(relativePath));
            }

            return relativePath.Replace("\\", "/").TrimStart('/');
        }
        #endregion
    }
}