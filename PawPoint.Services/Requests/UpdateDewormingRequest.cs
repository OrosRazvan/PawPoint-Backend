using PawPoint.DB.Enums;

namespace PawPoint.Services.Requests
{
    public sealed class UpdateDewormingRequest
    {
        public DewormingTypeEnum? Type { get; set; }

        public int? IntervalDays { get; set; }

        public string? Notes { get; set; }
    }
}
