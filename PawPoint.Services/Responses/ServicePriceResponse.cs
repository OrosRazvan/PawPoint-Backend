using PawPoint.DB.Enums;

namespace PawPoint.Services.Responses
{
    public sealed record ServicePriceResponse(
        decimal Price,
        Currency Currency
    );
}