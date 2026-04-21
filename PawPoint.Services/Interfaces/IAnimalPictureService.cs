using Microsoft.AspNetCore.Http;

namespace PawPoint.Services.Interfaces
{
    public interface IAnimalPictureService
    {
        Task<string> UploadAsync(int userId, int animalId, IFormFile file);
        Task DeleteAsync(string relativePath);
    }
}