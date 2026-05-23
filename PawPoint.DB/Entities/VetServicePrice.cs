using System.ComponentModel.DataAnnotations;
using PawPoint.DB.Enums;

namespace PawPoint.DB.Entities
{
    public class VetServicePrice
    {
        public int Id { get; set; }
        public int VetCabinetId { get; set; }
        public VetCabinet VetCabinet { get; set; } = null!;

        [MaxLength(50)]
        public required string ServiceType { get; set; }
        public DewormingTypeEnum? DewormingType { get; set; }
        public VaccineType? VaccineType { get; set; }
        public decimal Price { get; set; }
        public Currency Currency { get; set; } = Currency.Eur;
    }
}
