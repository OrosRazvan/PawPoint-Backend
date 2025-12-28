namespace PawPoint.Services.Interfaces
{
    public interface IProfilePictureUrlFactory
    {
        string BuildPublicUrl(string? relativePath);
    }
}
