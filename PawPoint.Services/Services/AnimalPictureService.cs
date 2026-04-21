using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PawPoint.Services.Interfaces;

namespace PawPoint.Services.Services
{
    public sealed class AnimalPictureService(
        [FromKeyedServices("animal-pics")] BlobContainerClient container)
        : IAnimalPictureService
    {
        private readonly BlobContainerClient _container = container;

        public async Task<string> UploadAsync(int userId, int animalId, IFormFile file)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (animalId <= 0) throw new ArgumentOutOfRangeException(nameof(animalId));
            if (file is null) throw new ArgumentNullException(nameof(file));
            if (file.Length <= 0) throw new ArgumentOutOfRangeException(nameof(file), "File is empty.");

            var contentType = file.ContentType?.ToLowerInvariant();
            if (contentType is not ("image/jpeg" or "image/png" or "image/webp"))
                throw new NotSupportedException($"Unsupported content type: {file.ContentType}");

            const long maxBytes = 5 * 1024 * 1024;
            if (file.Length > maxBytes)
                throw new ArgumentOutOfRangeException(nameof(file), "File too large.");

            await _container.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var ext = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => Path.GetExtension(file.FileName)
            };

            var blobName =
                $"users/{userId}/animals/{animalId}/img-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}{ext}";

            var blob = _container.GetBlobClient(blobName);

            await using var stream = file.OpenReadStream();
            await blob.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = file.ContentType,
                        CacheControl = "public, max-age=31536000"
                    }
                });

            return blobName;
        }

        public async Task DeleteAsync(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Relative path required.", nameof(relativePath));

            var blob = _container.GetBlobClient(relativePath);
            await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
        }
    }
}