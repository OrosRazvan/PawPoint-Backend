using PawPoint.DB.Enums;

namespace PawPoint.Services.Requests
{
    public sealed class ServicePriceRequest
    {
        public int VetCabinetId { get; set; }
        public required string ServiceType { get; set; }

        public DewormingTypeEnum? DewormingType { get; set; }
        public VaccineType? VaccineType { get; set; }
    }
}