using Microsoft.AspNetCore.Http;

namespace PawPoint.Services.Interfaces
{
    public interface IProfilePictureService
    {
        Task<string> UploadAsync(int userId, IFormFile file);
        Task DeleteAsync(string relativePath);
    }
}
