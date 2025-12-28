namespace PawPoint.Common.Helpers
{
    public class JwtSettings()
    {
        public required string AccessSecret { get; set; }
        public required string RefreshSecret { get; set; }
        public required string Issuer { get; set; }
        public required string Audience { get; set; }
        public required int AccessExpiresInMinutes { get; set; }
        public required int RefreshExpiresInMinutes { get; set; }
    }
}
