using PawPoint.Services.Requests;
using PawPoint.Services.Responses;
using System.Threading.Tasks;

namespace PawPoint.Services.Interfaces
{
    public interface IUserSettingsService
    {
        Task<UserSettingsResponse> GetAsync(int userId);
        Task<UserSettingsResponse> UpdateAsync(int userId, UserSettingsUpdateRequest request);
    }
}
