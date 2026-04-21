using Microsoft.Extensions.Configuration;

namespace PawPoint.Services.Services
{
    public sealed class AnimalPictureUrlFactory(IConfiguration configuration)
    {
        private readonly string _baseUrl =
            configuration["Storage:AnimalBaseUrl"]
            ?? throw new InvalidOperationException("Missing Storage:AnimalBaseUrl");

        public string? Build(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            if (Uri.TryCreate(relativePath, UriKind.Absolute, out _))
                return relativePath;

            return $"{_baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
        }
    }
}