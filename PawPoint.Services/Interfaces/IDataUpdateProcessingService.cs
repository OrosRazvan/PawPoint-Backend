using PawPoint.Services.Responses;

namespace PawPoint.Services.Interfaces
{
    public interface IDataUpdateProcessingService
    {
        public Task<ProcessResponse> ProcessDataUpdateBlobAsync(string blobName);
    }
}
