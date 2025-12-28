using PawPoint.Common.Helpers;
using PawPoint.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace PawPoint.Services.Services
{
    public sealed class ProfilePictureUrlFactory(IOptions<StorageSettings> storageOptions)
        : IProfilePictureUrlFactory
    {
        private readonly string _baseUrl = storageOptions.Value.BaseUrl?.Trim() ?? string.Empty;

        public string BuildPublicUrl(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            if (Uri.TryCreate(relativePath, UriKind.Absolute, out _))
            {
                return relativePath;
            }

            if (string.IsNullOrWhiteSpace(_baseUrl))
            {
                throw new InvalidOperationException("Storage.BaseUrl is not configured.");
            }

            var baseUrl = _baseUrl.EndsWith("/", StringComparison.Ordinal) ? _baseUrl : _baseUrl + "/";
            var path = relativePath.Replace("\\", "/").TrimStart('/');

            return baseUrl + path;
        }
    }
}

