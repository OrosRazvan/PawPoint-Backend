using PawPoint.DB.Enums;

namespace PawPoint.Services.Requests
{
    public sealed class CreateDewormingRequest
    {
        public int AnimalId { get; set; }

        public DewormingTypeEnum Type { get; set; }

        public int VetCabinetId { get; set; }
        public int VetTimeSlotId { get; set; }

        public string? Notes { get; set; }
    }
}