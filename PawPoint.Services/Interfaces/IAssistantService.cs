using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services.Interfaces;

public interface IAssistantService
{
    Task<AssistantMessageResponse> ProcessMessageAsync(
        AssistantMessageRequest request,
        CancellationToken cancellationToken = default);
}