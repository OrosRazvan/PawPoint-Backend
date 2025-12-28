namespace PawPoint.Services.Interfaces
{
    public interface IEmailService
    {
        public Task SendEmailAsync(string toEmail, string title, string textBody, string htmlBody);
    }
}
