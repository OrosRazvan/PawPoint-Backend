namespace PawPoint.Services.Interfaces
{
    public interface IEmailIndexService
    {
        string Normalize(string email);
        string ComputeHash(string normalized);
    }
}
