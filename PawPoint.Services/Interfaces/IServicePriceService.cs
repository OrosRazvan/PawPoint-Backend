using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Interfaces
{
    public interface IServicePriceService
    {
        Task<ServicePriceResponse?> GetPriceAsync(ServicePriceRequest request);
    }
}