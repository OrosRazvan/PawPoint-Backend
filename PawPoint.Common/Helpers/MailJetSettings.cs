namespace PawPoint.Common.Helpers
{
    public class MailJetSettings
    {
        public required string Endpoint { get; set; }
        public required string Key { get; set; }
        public required string Secret { get; set; }
        public required string SenderEmail { get; set; }
        public required string SenderName { get; set; }
    }
}
