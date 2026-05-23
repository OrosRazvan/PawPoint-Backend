using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Services
{
    public sealed class ServicePriceService(Context db) : IServicePriceService
    {
        private readonly Context _db = db;

        public async Task<ServicePriceResponse?> GetPriceAsync(ServicePriceRequest request)
        {
            if (request.VetCabinetId <= 0)
                throw new ArgumentException("VetCabinetId is required.");

            if (string.IsNullOrWhiteSpace(request.ServiceType))
                throw new ArgumentException("ServiceType is required.");

            var query = _db.VetServicePrices
                .AsNoTracking()
                .Where(x =>
                    x.VetCabinetId == request.VetCabinetId &&
                    x.ServiceType == request.ServiceType.Trim());

            if (request.DewormingType.HasValue)
                query = query.Where(x => x.DewormingType == request.DewormingType.Value);

            if (request.VaccineType.HasValue)
                query = query.Where(x => x.VaccineType == request.VaccineType.Value);

            var price = await query.FirstOrDefaultAsync();

            if (price is null)
                return null;

            return new ServicePriceResponse(
                price.Price,
                price.Currency
            );
        }
    }
}