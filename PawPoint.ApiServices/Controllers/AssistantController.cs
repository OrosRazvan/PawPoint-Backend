using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;

namespace PawPoint.ApiServices.Controllers;

[Authorize]
[Route("[controller]")]
public class AssistantController : BaseApiController
{
    private readonly IAssistantService _assistantService;

    public AssistantController(
        IAssistantService assistantService,
        IIdentityService identityService)
        : base(identityService)
    {
        _assistantService = assistantService;
    }

    [HttpPost("message")]
    public async Task<IActionResult> Message(
        [FromBody] AssistantMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Mesajul este obligatoriu.");

        request.UserId = userId;

        var response = await _assistantService.ProcessMessageAsync(
            request,
            cancellationToken);

        return Ok(response);
    }
}