namespace PawPoint.Services.Interfaces
{
    public interface IPiiEncryptionService
    {
        string Encrypt(string plaintext);
        string Decrypt(string payloadBase64);
    }
}
